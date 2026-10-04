#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    // TODO: Burst application, toposort to resolve chains of preview propagation
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

        // InitialActions has already selected the final initially-active action for each
        // ReactiveMoveTarget (ToMove, Channel). Do not re-resolve winners from Shapes here.
        private static List<ReactiveMoveAction> GetWinningActions(ComputeContext context, GameObject avatarRoot)
        {
            var actions = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot).InitialActions.Values
                .OfType<ReactiveMoveAction>()
                .Where(action => action.ToMove != null)
                .ToList();
            OrderWinningActions(context, avatarRoot, actions);
            return actions;
        }

        private static void OrderWinningActions(
            ComputeContext context,
            GameObject avatarRoot,
            List<ReactiveMoveAction> actions
        )
        {
            var hierarchyOrder = new Dictionary<Transform, int>();
            var hierarchyIndex = 0;
            foreach (var transform in context.GetComponentsInChildren<Transform>(avatarRoot, true))
            {
                hierarchyOrder[transform] = hierarchyIndex++;
            }

            // Keep targets outside the avatar root in their analyzer discovery order.
            var fallbackOrder = new Dictionary<Transform, int>();
            for (var index = 0; index < actions.Count; index++)
            {
                fallbackOrder.TryAdd(actions[index].ToMove, index);
            }

            actions.Sort((left, right) =>
            {
                var leftInHierarchy = hierarchyOrder.TryGetValue(left.ToMove, out var leftHierarchyIndex);
                var rightInHierarchy = hierarchyOrder.TryGetValue(right.ToMove, out var rightHierarchyIndex);

                var targetOrder = leftInHierarchy && rightInHierarchy
                    ? leftHierarchyIndex.CompareTo(rightHierarchyIndex)
                    : leftInHierarchy
                        ? -1
                        : rightInHierarchy
                            ? 1
                            : fallbackOrder[left.ToMove].CompareTo(fallbackOrder[right.ToMove]);
                if (targetOrder != 0) return targetOrder;

                return ChannelOrder(left).CompareTo(ChannelOrder(right));
            });
        }

        private static int ChannelOrder(ReactiveMoveAction action)
        {
            return action.Channel switch
            {
                ReactiveMoveChannels.Position => 0,
                ReactiveMoveChannels.Rotation => 1,
                ReactiveMoveChannels.Scale => 2,
                _ => 3
            };
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
            return actions.Where(action => !action.FixToWorld && action.WhereTo != null)
                .Select(action =>
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

            // Analyzer-selected winners in cached hierarchy traversal order.
            private List<ReactiveMoveAction> _winningActions;
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
                _winningActions = GetWinningActions(context, avatarRoot);
                _referencedTransforms = GetReferencedTransforms(context, avatarRoot);
                _targetWorldTransforms = ObserveTargetTransforms(context, _winningActions);
                RequestShadowBones(context, proxyPairs.ToList());
            }

            public Task<IRenderFilterNode> Refresh(
                IEnumerable<(Renderer, Renderer)> proxyPairs,
                ComputeContext context,
                RenderAspects updatedAspects
            )
            {
                if (_avatarRoot == null) return Task.FromResult<IRenderFilterNode>(null);

                var winningActions = GetWinningActions(context, _avatarRoot);
                var referencedTransforms = GetReferencedTransforms(context, _avatarRoot);
                var targetWorldTransforms = ObserveTargetTransforms(context, winningActions);
                var actionsChanged = !ActionsEqual(_winningActions, winningActions);
                var stateChanged = actionsChanged
                                   || !_referencedTransforms.SequenceEqual(referencedTransforms)
                                   || !_targetWorldTransforms.SequenceEqual(targetWorldTransforms);

                if (actionsChanged)
                {
                    // The prior pipeline remains visible while this refresh builds. Update its node so the next
                    // preview frame applies the new state without waiting for the replacement pipeline.
                    _winningActions = winningActions;
                }

                if (ReferenceEquals(PreviewContext.Instance, _previewContext) && !stateChanged)
                {
                    WhatChanged = 0;
                    RequestShadowBones(context, proxyPairs.ToList());
                    return Task.FromResult<IRenderFilterNode>(this);
                }

                return Task.FromResult<IRenderFilterNode>(new Node(context, _avatarRoot, proxyPairs));
            }

            public void OnFrameGroup()
            {
                // Winners are cached in target hierarchy traversal order.
                foreach (var action in _winningActions)
                {
                    if (action.ToMove == null) continue;

                    var moved = _previewContext.ShadowBoneManager.GetBone(action.ToMove);
                    var target = action.FixToWorld
                        ? action.ToMove
                        : action.WhereTo != null
                            ? _previewContext.ShadowBoneManager.GetBone(action.WhereTo)
                            : null;
                    if (moved == null || target == null) continue;

                    if (action.SetsPosition) moved.position = target.position;
                    if (action.SetsRotation) moved.rotation = target.rotation;
                    if (action.SetsScale) SetWorldScale(moved, target.lossyScale);
                }
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
            }

            private void RequestShadowBones(ComputeContext context, List<(Renderer, Renderer)> proxyPairs)
            {
                var movedTransforms = _winningActions.Select(action => action.ToMove)
                    .Concat(_referencedTransforms)
                    .Where(transform => transform != null)
                    .Distinct()
                    .ToList();

                foreach (var transform in movedTransforms)
                {
                    _previewContext.ShadowBoneManager.GetBone(transform);
                    context.ObserveTransformPosition(transform);
                }

                foreach (var (original, proxy) in proxyPairs)
                {
                    if (original == null || proxy == null) continue;

                    RegisterAffectedReference(context, original, original.transform, movedTransforms);
                    if (original is SkinnedMeshRenderer skinned)
                    {
                        foreach (var bone in context.Observe(skinned, smr => smr.bones, Enumerable.SequenceEqual))
                        {
                            RegisterAffectedReference(context, original, bone, movedTransforms);
                        }

                        RegisterAffectedReference(
                            context,
                            original,
                            context.Observe(skinned, smr => smr.rootBone),
                            movedTransforms
                        );
                    }

                    RegisterAffectedReference(
                        context,
                        original,
                        context.Observe(original, renderer => renderer.probeAnchor),
                        movedTransforms
                    );
                }
            }

            private void RegisterAffectedReference(
                ComputeContext context,
                Renderer renderer,
                Transform? reference,
                IEnumerable<Transform> movedTransforms
            )
            {
                if (reference == null ||
                    !movedTransforms.Any(transform => reference == transform || reference.IsChildOf(transform))) return;

                context.ObserveTransformPosition(reference);
                var replacement = _previewContext.ShadowBoneManager.GetBone(reference);
                _previewContext.ShadowBoneManager.ReplaceBone(renderer, reference, replacement);
            }

            private static bool ActionsEqual(
                IReadOnlyList<ReactiveMoveAction> left,
                IReadOnlyList<ReactiveMoveAction> right
            )
            {
                return left.Count == right.Count && left.Zip(right, (a, b) => a.Equals(b)).All(equal => equal);
            }

            private static void SetWorldScale(Transform transform, Vector3 targetScale)
            {
                var localScale = transform.localScale;
                var currentScale = transform.lossyScale;
                if (localScale.x == 0 || localScale.y == 0 || localScale.z == 0)
                {
                    transform.localScale = Vector3.one;
                    localScale = Vector3.one;
                    currentScale = transform.lossyScale;
                }

                transform.localScale = new Vector3(
                    currentScale.x == 0 ? 0 : localScale.x * targetScale.x / currentScale.x,
                    currentScale.y == 0 ? 0 : localScale.y * targetScale.y / currentScale.y,
                    currentScale.z == 0 ? 0 : localScale.z * targetScale.z / currentScale.z
                );
            }
        }
    }
}