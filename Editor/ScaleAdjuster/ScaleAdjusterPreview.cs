#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPreview : IRenderFilter
    {
        private static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Scale Adjuster",
            qualifiedName: "nadena.dev.modular-avatar/ScaleAdjusterPreview",
            true
        );

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return EnableNode;
        }

        public bool IsEnabled(ComputeContext context)
        {
            return context.Observe(EnableNode.IsEnabled);
        }

        private static GameObject FindAvatarRootObserving(ComputeContext ctx, GameObject ptr)
        {
            while (ptr != null)
            {
                ctx.Observe(ptr);
                var xform = ptr.transform;
                if (RuntimeUtil.IsAvatarRoot(xform)) return ptr;

                ptr = xform.parent?.gameObject;
            }

            return null;
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext ctx)
        {
            var avatarToRenderer = new Dictionary<GameObject, HashSet<Renderer>>();

            foreach (var root in ctx.GetAvatarRoots())
            {
                if (ctx.ActiveInHierarchy(root) is false)
                {
                    continue;
                }

                if (ctx.GetComponentsInChildren<ModularAvatarScaleAdjuster>(root, true).Length == 0)
                {
                    continue;
                }

                if (ctx.GetAvatarRoot(root?.transform?.parent?.gameObject) != null)
                {
                    continue; // nested avatar descriptor
                }

                var renderers = new HashSet<Renderer>();
                avatarToRenderer.Add(root, renderers);

                foreach (var renderer in ctx.GetComponentsInChildren<SkinnedMeshRenderer>(root, true))
                {
                    renderers.Add(renderer);
                }
            }

            return avatarToRenderer.Select(kvp => RenderGroup.For(kvp.Value).WithData(kvp.Key)).ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            return Task.FromResult<IRenderFilterNode>(new ScaleAdjusterPreviewNode(context, group, proxyPairs));
        }
    }

    internal class ScaleAdjusterPreviewNode : IRenderFilterNode
    {
        private readonly GameObject SourceAvatarRoot;
        private readonly PreviewContext _previewContext;
        private readonly HashSet<Transform> _sourceBones;
        private readonly HashSet<Renderer> _sourceRenderers;
        private readonly Dictionary<Renderer, Transform[]> _rendererBones;
        private readonly Dictionary<ModularAvatarScaleAdjuster, Transform> _scaleAdjusters = new();
        private readonly Dictionary<ModularAvatarScaleAdjuster, Vector3> _scaleAdjusterValues;

        public ScaleAdjusterPreviewNode(
            ComputeContext context,
            RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs
        ) : this(
            context,
            group.GetData<GameObject>(),
            proxyPairs
        )
        {
        }

        private ScaleAdjusterPreviewNode(
            ComputeContext context,
            GameObject avatarRoot,
            IEnumerable<(Renderer, Renderer)> proxyPairs
        )
        {
            _previewContext = PreviewContext.Instance;
            var proxyPairList = proxyPairs.ToList();

            SourceAvatarRoot = avatarRoot;
            _sourceRenderers = proxyPairList
                .Select(pair => pair.Item1)
                .Where(renderer => renderer != null)
                .ToHashSet();
            _rendererBones = GetRendererBones(context, proxyPairList);
            _sourceBones = _rendererBones.Values.SelectMany(bones => bones).Where(bone => bone != null).ToHashSet();
            _scaleAdjusterValues = GetScaleAdjusterValues(context);

            var replacementBones = new Dictionary<Transform, Transform>();
            foreach (var sourceBone in _sourceBones)
            {
                replacementBones[sourceBone] = _previewContext.ShadowBoneManager.GetBone(sourceBone);
            }

            foreach (var (scaleAdjuster, scale) in _scaleAdjusterValues)
            {
                var proxyShadow = new GameObject("[Scale Adjuster Proxy]").transform;
                proxyShadow.SetParent(replacementBones[scaleAdjuster.transform], false);
                proxyShadow.localPosition = Vector3.zero;
                proxyShadow.localRotation = Quaternion.identity;
                proxyShadow.localScale = scale;

                _scaleAdjusters[scaleAdjuster] = proxyShadow;
                replacementBones[scaleAdjuster.transform] = proxyShadow;
            }

            RegisterBoneReplacements(proxyPairList, replacementBones);
        }

        private Dictionary<Renderer, Transform[]> GetRendererBones(ComputeContext context,
            List<(Renderer, Renderer)> proxyPairs)
        {
            var rendererBones = new Dictionary<Renderer, Transform[]>();
            foreach (var (original, proxy) in proxyPairs)
            {
                if (original == null || proxy is not SkinnedMeshRenderer smr) continue;

                var bones = context.Observe(smr, smr_ => smr_.bones, Enumerable.SequenceEqual).ToArray();
                rendererBones[original] = bones;
            }

            return rendererBones;
        }

        private Dictionary<Transform, Transform> GetReplacementBones()
        {
            var replacementBones = new Dictionary<Transform, Transform>();
            foreach (var sourceBone in _sourceBones)
            {
                replacementBones[sourceBone] = _previewContext.ShadowBoneManager.GetBone(sourceBone);
            }

            foreach (var (scaleAdjuster, proxyBone) in _scaleAdjusters)
            {
                if (scaleAdjuster != null && proxyBone != null)
                {
                    replacementBones[scaleAdjuster.transform] = proxyBone;
                }
            }

            return replacementBones;
        }

        private Dictionary<ModularAvatarScaleAdjuster, Vector3> GetScaleAdjusterValues(ComputeContext context)
        {
            return context.GetComponentsInChildren<ModularAvatarScaleAdjuster>(SourceAvatarRoot, true)
                .Where(scaleAdjuster => _sourceBones.Contains(scaleAdjuster.transform))
                .ToDictionary(
                    scaleAdjuster => scaleAdjuster,
                    scaleAdjuster => context.Observe(scaleAdjuster, adjuster => adjuster.Scale)
                );
        }

        private void RegisterBoneReplacements(List<(Renderer, Renderer)> proxyPairs,
            Dictionary<Transform, Transform> replacementBones)
        {
            foreach (var (renderer, proxy) in proxyPairs)
            {
                if (renderer == null || proxy is not SkinnedMeshRenderer
                                     || !_rendererBones.TryGetValue(renderer, out var bones))
                {
                    continue;
                }

                foreach (var sourceBone in bones.Where(bone => bone != null).Distinct())
                {
                    _previewContext.ShadowBoneManager.ReplaceBone(
                        renderer,
                        sourceBone,
                        replacementBones[sourceBone]
                    );
                }
            }
        }

        public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context,
            RenderAspects updatedAspects)
        {
            if (SourceAvatarRoot == null) return Task.FromResult<IRenderFilterNode>(null);

            var proxyPairList = proxyPairs.ToList();
            var sourceRenderers = proxyPairList
                .Select(pair => pair.Item1)
                .Where(renderer => renderer != null)
                .ToHashSet();
            var rendererBones = GetRendererBones(context, proxyPairList);
            var scaleAdjusterValues = GetScaleAdjusterValues(context);

            if (ReferenceEquals(PreviewContext.Instance, _previewContext)
                && _sourceRenderers.SetEquals(sourceRenderers)
                && RendererBonesEqual(_rendererBones, rendererBones)
                && ScaleAdjusterValuesEqual(_scaleAdjusterValues, scaleAdjusterValues))
            {
                WhatChanged = 0;
                RegisterBoneReplacements(proxyPairList, GetReplacementBones());
                return Task.FromResult<IRenderFilterNode>(this);
            }

            return Task.FromResult<IRenderFilterNode>(
                new ScaleAdjusterPreviewNode(context, SourceAvatarRoot, proxyPairList)
            );
        }

        private static bool RendererBonesEqual(Dictionary<Renderer, Transform[]> left,
            Dictionary<Renderer, Transform[]> right)
        {
            if (left.Count != right.Count) return false;

            foreach (var (renderer, bones) in left)
            {
                if (!right.TryGetValue(renderer, out var otherBones)
                    || !bones.SequenceEqual(otherBones))
                {
                    return false;
                }
            }

            return true;
        }

        private Dictionary<Transform, Matrix4x4> CaptureSourceBoneWorldTransforms(ComputeContext context)
        {
            var worldTransforms = new Dictionary<Transform, Matrix4x4>();
            foreach (var source in _sourceBones)
            {
                context.ObserveTransformPosition(source);
                worldTransforms[source] = source.localToWorldMatrix;
            }

            return worldTransforms;
        }

        private static bool ScaleAdjusterValuesEqual(
            Dictionary<ModularAvatarScaleAdjuster, Vector3> left,
            Dictionary<ModularAvatarScaleAdjuster, Vector3> right)
        {
            if (left.Count != right.Count) return false;

            foreach (var (scaleAdjuster, scale) in left)
            {
                if (!right.TryGetValue(scaleAdjuster, out var otherScale) || !scale.Equals(otherScale))
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyScaleAdjusterValues()
        {
            foreach (var (scaleAdjuster, transform) in _scaleAdjusters)
                if (scaleAdjuster != null && transform != null)
                    transform.localScale = scaleAdjuster.Scale;
        }

        public RenderAspects WhatChanged { get; private set; } = RenderAspects.Shapes;

        public void OnFrameGroup()
        {
            ApplyScaleAdjusterValues();
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
        }

        public void Dispose()
        {
            foreach (var transform in _scaleAdjusters.Values)
            {
                if (transform != null)
                {
                    Object.DestroyImmediate(transform.gameObject);
                }
            }
        }
    }
}
