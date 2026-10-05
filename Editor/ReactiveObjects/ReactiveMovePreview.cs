#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class ReactiveMovePreview : IRenderFilter
    {
        private static readonly TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Reactive Move",
            "nadena.dev.modular-avatar/ReactiveMovePreview"
        );

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return EnableNode;
        }

        public bool IsEnabled(ComputeContext context)
        {
            return context.Observe(EnableNode.IsEnabled);
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var groups = ImmutableList.CreateBuilder<RenderGroup>();
            foreach (var avatarRoot in context.GetAvatarRoots())
            {
                if (avatarRoot == null || !context.ActiveInHierarchy(avatarRoot)) continue;

                var referencedTransforms = GetReferencedTransforms(context, avatarRoot);
                if (referencedTransforms.Count == 0) continue;

                var renderers = context.GetComponentsInChildren<Renderer>(avatarRoot, true)
                    .Where(renderer => renderer is MeshRenderer or SkinnedMeshRenderer)
                    .ToHashSet();
                if (renderers.Count == 0) continue;

                groups.Add(RenderGroup.For(renderers).WithData(avatarRoot));
            }

            return groups.ToImmutable();
        }

        public Task<IRenderFilterNode> Instantiate(
            RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context
        )
        {
            return Task.FromResult<IRenderFilterNode>(new Node(context, group.GetData<GameObject>(), proxyPairs));
        }

        private static List<ReactiveMoveAction> GetInitialActions(ComputeContext context, GameObject avatarRoot)
        {
            return ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot).InitialActions.Values
                .OfType<ReactiveMoveAction>()
                .Where(action => action.ToMove != null && action.WhereTo != null)
                .ToList();
        }

        private static List<Transform> GetReferencedTransforms(ComputeContext context, GameObject avatarRoot)
        {
            return ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot).Shapes.Values
                .SelectMany(property => property.actionGroups)
                .Select(rule => rule.Action)
                .OfType<ReactiveMoveAction>()
                .Select(action => action.ToMove)
                .Where(transform => transform != null)
                .Distinct()
                .ToList();
        }

        private static List<Matrix4x4> ObserveTargetTransforms(
            ComputeContext context,
            IEnumerable<ReactiveMoveAction> actions
        )
        {
            return actions.Select(action =>
                {
                    context.ObserveTransformPosition(action.WhereTo);
                    return action.WhereTo.localToWorldMatrix;
                })
                .ToList();
        }

        private sealed class Node : IRenderFilterNode
        {
            private readonly GameObject _avatarRoot;
            private readonly PreviewContext _previewContext;
            private List<ReactiveMoveAction> _actions;
            private readonly List<Transform> _referencedTransforms;
            private readonly List<Matrix4x4> _targetWorldTransforms;

            public RenderAspects WhatChanged { get; private set; } = RenderAspects.Shapes;

            internal Node(
                ComputeContext context,
                GameObject avatarRoot,
                IEnumerable<(Renderer, Renderer)> proxyPairs
            )
            {
                _avatarRoot = avatarRoot;
                _previewContext = PreviewContext.Instance;
                _actions = GetInitialActions(context, avatarRoot);
                _referencedTransforms = GetReferencedTransforms(context, avatarRoot);
                _targetWorldTransforms = ObserveTargetTransforms(context, _actions);
                RequestShadowBones(_referencedTransforms);
            }

            public Task<IRenderFilterNode> Refresh(
                IEnumerable<(Renderer, Renderer)> proxyPairs,
                ComputeContext context,
                RenderAspects updatedAspects
            )
            {
                if (_avatarRoot == null) return Task.FromResult<IRenderFilterNode>(null);

                var actions = GetInitialActions(context, _avatarRoot);
                var referencedTransforms = GetReferencedTransforms(context, _avatarRoot);
                var targetWorldTransforms = ObserveTargetTransforms(context, actions);
                var actionsChanged = !ActionsEqual(_actions, actions);
                var stateChanged = actionsChanged
                                   || !_referencedTransforms.SequenceEqual(referencedTransforms)
                                   || !_targetWorldTransforms.SequenceEqual(targetWorldTransforms);

                if (actionsChanged)
                {
                    // The prior pipeline remains visible while this refresh builds. Update its node so the next
                    // preview frame applies the new state without waiting for the replacement pipeline.
                    _actions = actions;
                }

                if (ReferenceEquals(PreviewContext.Instance, _previewContext) && !stateChanged)
                {
                    WhatChanged = 0;
                    RequestShadowBones(_referencedTransforms);
                    return Task.FromResult<IRenderFilterNode>(this);
                }

                return Task.FromResult<IRenderFilterNode>(new Node(context, _avatarRoot, proxyPairs));
            }

            public void OnFrameGroup()
            {
                foreach (var action in _actions)
                {
                    if (action.ToMove == null || action.WhereTo == null) continue;

                    var moved = _previewContext.ShadowBoneManager.GetBone(action.ToMove);
                    var target = _previewContext.ShadowBoneManager.GetBone(action.WhereTo);
                    if (moved == null || target == null) continue;

                    if (action.SetsPosition) moved.position = target.position;
                    if (action.SetsRotation) moved.rotation = target.rotation;
                    if (action.SetsScale) SetWorldScale(moved, target.localToWorldMatrix);
                }
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
            }

            private void RequestShadowBones(IEnumerable<Transform> referencedTransforms)
            {
                foreach (var transform in referencedTransforms)
                {
                    _previewContext.ShadowBoneManager.GetBone(transform);
                }
            }

            private static bool ActionsEqual(
                IReadOnlyList<ReactiveMoveAction> left,
                IReadOnlyList<ReactiveMoveAction> right
            )
            {
                return left.Count == right.Count && left.Zip(right, (a, b) => a.Equals(b)).All(equal => equal);
            }

            private static void SetWorldScale(Transform transform, Matrix4x4 targetWorldMatrix)
            {
                var parentInverseMatrix = transform.parent?.worldToLocalMatrix ?? Matrix4x4.identity;
                var localTransformWithoutScale = Matrix4x4.TRS(
                    transform.localPosition,
                    transform.localRotation,
                    Vector3.one
                );
                transform.localScale =
                    (localTransformWithoutScale * parentInverseMatrix * targetWorldMatrix).lossyScale;
            }
        }
    }
}