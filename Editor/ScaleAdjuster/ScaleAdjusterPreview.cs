#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using Unity.Burst;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;

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
        
        [InitializeOnLoadMethod]
        private static void StaticInit()
        {
        }

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
            var avatarToRenderer =
                new Dictionary<GameObject, HashSet<Renderer>>();

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
        private readonly GameObject VirtualAvatarRoot;

        private TransformAccessArray _srcBones;
        private TransformAccessArray _dstBones;

        private NativeArray<bool> _boneIsValid;
        private NativeArray<BoneState> _boneStates;

        // Map from bones found in initial proxy state to shadow bones
        private readonly Dictionary<Transform, Transform> _shadowBoneMap;
        private readonly Dictionary<Transform, Matrix4x4> _sourceBoneWorldTransforms;
        private readonly HashSet<Transform> _sourceBones;
        private readonly HashSet<Renderer> _sourceRenderers;
        private readonly Dictionary<Renderer, Transform[]> _rendererBones;

        // Map from bones found in initial proxy state to shadow bones (with scale adjuster bones substituted)
        private readonly Dictionary<Transform, Transform> _finalBonesMap = new();

        private readonly Dictionary<ModularAvatarScaleAdjuster, Transform> _scaleAdjusters =
            new();

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
            var proxyPairList = proxyPairs.ToList();

            SourceAvatarRoot = avatarRoot;

            var scene = NDMFPreviewSceneManager.GetPreviewScene();
            var priorScene = SceneManager.GetActiveScene();

            _sourceRenderers = proxyPairList
                .Select(pair => pair.Item1)
                .Where(renderer => renderer != null)
                .ToHashSet();
            _rendererBones = GetRendererBones(context, proxyPairList);
            _sourceBones = _rendererBones.Values.SelectMany(bones => bones).Where(bone => bone != null).ToHashSet();
            var bones = _sourceBones.OrderBy(k => k.gameObject.name).ToArray();

            Transform[] sourceBones;
            Transform[] destinationBones;
            try
            {
                SceneManager.SetActiveScene(scene);
                VirtualAvatarRoot = new GameObject(avatarRoot.name + " [ScaleAdjuster]");

                _shadowBoneMap = CreateShadowBones(bones);
                sourceBones = new Transform[_shadowBoneMap.Count];
                destinationBones = new Transform[_shadowBoneMap.Count];

                var i = 0;
                foreach (var (src, dst) in _shadowBoneMap)
                {
                    sourceBones[i] = src;
                    destinationBones[i] = dst;
                    i++;
                }
            }
            finally
            {
                SceneManager.SetActiveScene(priorScene);
            }

            _sourceBoneWorldTransforms = CaptureSourceBoneWorldTransforms(context);

            _srcBones = new TransformAccessArray(sourceBones);
            _dstBones = new TransformAccessArray(destinationBones);

            _boneIsValid = new NativeArray<bool>(sourceBones.Length, Allocator.Persistent);
            _boneStates = new NativeArray<BoneState>(sourceBones.Length, Allocator.Persistent);

            _scaleAdjusterValues = GetScaleAdjusterValues(context);
            FindScaleAdjusters();
            TransferBoneStates();
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

        private Dictionary<ModularAvatarScaleAdjuster, Vector3> GetScaleAdjusterValues(ComputeContext context)
        {
            return context.GetComponentsInChildren<ModularAvatarScaleAdjuster>(SourceAvatarRoot, true)
                .Where(scaleAdjuster => _sourceBones.Contains(scaleAdjuster.transform))
                .ToDictionary(
                    scaleAdjuster => scaleAdjuster,
                    scaleAdjuster => context.Observe(scaleAdjuster, adjuster => adjuster.Scale)
                );
        }

        private void FindScaleAdjusters()
        {
            _finalBonesMap.Clear();

            foreach (var kvp in _shadowBoneMap) _finalBonesMap[kvp.Key] = kvp.Value;

            foreach (var (scaleAdjuster, scale) in _scaleAdjusterValues)
            {
                var shadowBone = _shadowBoneMap[scaleAdjuster.transform];

                var proxyShadow = new GameObject("[Scale Adjuster Proxy]").transform;
                proxyShadow.SetParent(shadowBone, false);
                proxyShadow.localPosition = Vector3.zero;
                proxyShadow.localRotation = Quaternion.identity;
                proxyShadow.localScale = scale;

                _scaleAdjusters[scaleAdjuster] = proxyShadow;
                _finalBonesMap[scaleAdjuster.transform] = proxyShadow;
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

            if (_sourceRenderers.SetEquals(sourceRenderers)
                && RendererBonesEqual(_rendererBones, rendererBones)
                && SourceBoneWorldTransformsMatch(context)
                && ScaleAdjusterValuesEqual(_scaleAdjusterValues, scaleAdjusterValues))
            {
                // No meaningful changes; reuse this node as-is
                WhatChanged = 0;
                return Task.FromResult<IRenderFilterNode>(this);
            }

            // Build a new proxy hierarchy. Note that we need to do this even on scale adjuster value changes,
            // because downstream nodes might respond to bone scale changes.
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

        private bool SourceBoneWorldTransformsMatch(ComputeContext context)
        {
            foreach (var (source, worldTransform) in _sourceBoneWorldTransforms)
            {
                if (source == null) return false;

                context.ObserveTransformPosition(source);
                if (!source.localToWorldMatrix.Equals(worldTransform)) return false;
            }

            return true;
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

        private Dictionary<Transform, Transform> CreateShadowBones(Transform[] srcBones)
        {
            var srcToDst = new Dictionary<Transform, Transform>();

            for (var i = 0; i < srcBones.Length; i++) GetShadowBone(srcBones[i]);

            return srcToDst;

            Transform GetShadowBone(Transform srcBone)
            {
                if (srcBone == null) return null;
                if (srcToDst.TryGetValue(srcBone, out var dstBone)) return dstBone;

                var newBone = new GameObject(srcBone.name);
                ObjectRegistry.RegisterReplacedObject(srcBone.gameObject, newBone.gameObject);
                newBone.transform.SetParent(GetShadowBone(srcBone.parent) ?? VirtualAvatarRoot.transform);
                newBone.transform.localPosition = srcBone.localPosition;
                newBone.transform.localRotation = srcBone.localRotation;
                newBone.transform.localScale = srcBone.localScale;

                srcToDst[srcBone] = newBone.transform;

                return newBone.transform;
            }
        }

        private void TransferBoneStates()
        {
            var readTransforms = new ReadTransformsJob
            {
                BoneStates = _boneStates,
                BoneIsValid = _boneIsValid
            }.Schedule(_srcBones);

            var writeTransforms = new WriteBoneStatesJob
            {
                BoneStates = _boneStates,
                BoneIsValid = _boneIsValid
            }.Schedule(_dstBones, readTransforms);

            writeTransforms.Complete();
        }

        private struct BoneState
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 localScale;
        }
        
        [BurstCompile]
        private struct ReadTransformsJob : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<BoneState> BoneStates;
            [WriteOnly] public NativeArray<bool> BoneIsValid;

            public void Execute(int index, TransformAccess transform)
            {
                BoneIsValid[index] = transform.isValid;

                if (transform.isValid)
                {
                    BoneStates[index] = new BoneState
                    {
                        position = transform.position,
                        rotation = transform.rotation,
                        localScale = transform.localScale
                    };
                }
            }
        }

        [BurstCompile]
        private struct WriteBoneStatesJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<BoneState> BoneStates;
            [ReadOnly] public NativeArray<bool> BoneIsValid;

            public void Execute(int index, TransformAccess transform)
            {
                if (BoneIsValid[index])
                {
                    var state = BoneStates[index];

                    if (!ExactlyEqual(transform.position, state.position))
                    {
                        transform.position = state.position;
                    }

                    if (!ExactlyEqual(transform.rotation, state.rotation))
                    {
                        transform.rotation = state.rotation;
                    }

                    if (!ExactlyEqual(transform.localScale, state.localScale))
                    {
                        transform.localScale = state.localScale;
                    }
                }
            }

            private static bool ExactlyEqual(Vector3 left, Vector3 right)
            {
                return left.x == right.x && left.y == right.y && left.z == right.z;
            }

            private static bool ExactlyEqual(Quaternion left, Quaternion right)
            {
                return (left.x == right.x && left.y == right.y && left.z == right.z && left.w == right.w)
                       || (left.x == -right.x && left.y == -right.y && left.z == -right.z && left.w == -right.w);
            }
        }

        // Bone transforms affect the baked vertex positions consumed by downstream mesh-processing previews.
        public RenderAspects WhatChanged { get; private set; } = RenderAspects.Shapes;

        public void OnFrameGroup()
        {
            // Keep the visible preview moving while downstream nodes rebuild from a replacement node.
            TransferBoneStates();

            foreach (var (sa, xform) in _scaleAdjusters)
                if (sa != null && xform != null)
                    xform.localScale = sa.Scale;
        }
        
        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (proxy == null) return;

            var smr = proxy as SkinnedMeshRenderer;
            if (smr == null) return;

            smr.bones = smr.bones.Select(b => b == null ? null : _finalBonesMap.GetValueOrDefault(b, b)).ToArray();
        }
        
        public void Dispose()
        {
            Object.DestroyImmediate(VirtualAvatarRoot);

            _srcBones.Dispose();
            _dstBones.Dispose();
            _boneIsValid.Dispose();
            _boneStates.Dispose();
        }
    }
}
