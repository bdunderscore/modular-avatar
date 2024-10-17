#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.armature_lock;
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
            var scaleAdjusters = ctx.GetComponentsByType<ModularAvatarScaleAdjuster>();

            var avatarToRenderer =
                new Dictionary<GameObject, HashSet<Renderer>>();

            foreach (var root in ctx.GetAvatarRoots())
            {
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

                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    // For now, the preview system only supports MeshRenderer and SkinnedMeshRenderer
                    if (renderer is not MeshRenderer and not SkinnedMeshRenderer) continue;

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
        private readonly HashSet<Transform> _knownProxies = new();
        
        private readonly GameObject SourceAvatarRoot;
        private readonly GameObject VirtualAvatarRoot;

        private TransformAccessArray _srcBones;
        private TransformAccessArray _dstBones;

        private NativeArray<bool> _boneIsValid;
        private NativeArray<TransformState> _boneStates;

        // Map from bones found in initial proxy state to shadow bones
        private readonly Dictionary<Transform, Transform> _shadowBoneMap;

        // Map from bones found in initial proxy state to shadow bones (with scale adjuster bones substituted)
        private readonly Dictionary<Transform, Transform> _finalBonesMap = new();

        private readonly Dictionary<ModularAvatarScaleAdjuster, Transform> _scaleAdjusters =
            new();

        private Dictionary<Renderer, Transform[]> _rendererBoneStates = new();

        public ScaleAdjusterPreviewNode(ComputeContext context, RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs)
        {
            var proxyPairList = proxyPairs.ToList();

            var avatarRoot = group.GetData<GameObject>();
            SourceAvatarRoot = avatarRoot;

            var scene = NDMFPreviewSceneManager.GetPreviewScene();
            var priorScene = SceneManager.GetActiveScene();

            var bonesSet = GetSourceBonesSet(context, proxyPairList);
            var bones = bonesSet.OrderBy(k => k.gameObject.name).ToArray();

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

            _srcBones = new TransformAccessArray(sourceBones);
            _dstBones = new TransformAccessArray(destinationBones);

            _boneIsValid = new NativeArray<bool>(sourceBones.Length, Allocator.Persistent);
            _boneStates = new NativeArray<TransformState>(sourceBones.Length, Allocator.Persistent);

            FindScaleAdjusters(context);
            TransferBoneStates();
        }

        private HashSet<Transform> GetSourceBonesSet(ComputeContext context, List<(Renderer, Renderer)> proxyPairs)
        {
            var bonesSet = new HashSet<Transform>();
            foreach (var (_, r) in proxyPairs)
            {
                if (r == null) continue;

                var rootBone = context.Observe(r, r_ => (r_ as SkinnedMeshRenderer)?.rootBone) ?? r.transform;
                bonesSet.Add(rootBone);

                var smr = r as SkinnedMeshRenderer;
                if (smr == null) continue;

                foreach (var b in context.Observe(smr, smr_ => smr_.bones, Enumerable.SequenceEqual))
                {
                    if (b != null)
                    {
                        bonesSet.Add(b);
                    }
                }
            }

            return bonesSet;
        }

        private void FindScaleAdjusters(ComputeContext context)
        {
            _finalBonesMap.Clear();

            foreach (var (sa, proxy) in _scaleAdjusters.ToList())
            {
                // Note: We leak the proxy here, as destroying it can cause visual artifacts. They'll eventually get
                // cleaned up whenever the pipeline is fully reset, or when the scene is reloaded.
                if (sa == null)
                {
                    _scaleAdjusters.Remove(sa);
                }
            }

            _scaleAdjusters.Clear();

            foreach (var kvp in _shadowBoneMap) _finalBonesMap[kvp.Key] = kvp.Value;

            foreach (var scaleAdjuster in context.GetComponentsInChildren<ModularAvatarScaleAdjuster>(
                         SourceAvatarRoot.gameObject, true))
            {
                // If we don't find this in the map, we're not actually making use of this bone
                if (!_shadowBoneMap.TryGetValue(scaleAdjuster.transform, out var shadowBone)) continue;

                var proxyShadow = new GameObject("[Scale Adjuster Proxy]");
                proxyShadow.transform.SetParent(shadowBone);
                proxyShadow.transform.localPosition = Vector3.zero;
                proxyShadow.transform.localRotation = Quaternion.identity;
                proxyShadow.transform.localScale = scaleAdjuster.Scale;

                _scaleAdjusters[scaleAdjuster] = proxyShadow.transform;
                _finalBonesMap[scaleAdjuster.transform] = proxyShadow.transform;
            }
        }

        public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context,
            RenderAspects updatedAspects)
        {
            if (SourceAvatarRoot == null) return Task.FromResult<IRenderFilterNode>(null);

            // Clean any destroyed objects out of _knownProxies to avoid growing this set indefinitely
            _knownProxies.RemoveWhere(p => p == null);

            var proxyPairList = proxyPairs.ToList();

            if (!GetSourceBonesSet(context, proxyPairList).SetEquals(_shadowBoneMap.Keys))
                return Task.FromResult<IRenderFilterNode>(null);

            FindScaleAdjusters(context);
            TransferBoneStates();

            return Task.FromResult<IRenderFilterNode>(this);
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

        [BurstCompile]
        private struct ReadTransformsJob : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<TransformState> BoneStates;
            [WriteOnly] public NativeArray<bool> BoneIsValid;

            public void Execute(int index, TransformAccess transform)
            {
                BoneIsValid[index] = transform.isValid;

                if (transform.isValid)
                {
                    BoneStates[index] = new TransformState
                    {
                        localPosition = transform.position,
                        localRotation = transform.rotation,
                        localScale = transform.localScale
                    };
                }
            }
        }

        [BurstCompile]
        private struct WriteBoneStatesJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<TransformState> BoneStates;
            [ReadOnly] public NativeArray<bool> BoneIsValid;

            public void Execute(int index, TransformAccess transform)
            {
                if (BoneIsValid[index])
                {
                    var state = BoneStates[index];
                    transform.position = state.localPosition;
                    transform.rotation = state.localRotation;
                    transform.localScale = state.localScale;
                }
            }
        }

        public RenderAspects WhatChanged => RenderAspects.Shapes;

        public void OnFrameGroup()
        {
            TransferBoneStates();

            foreach (var (sa, xform) in _scaleAdjusters)
                if (sa != null && xform != null)
                    xform.localScale = sa.Scale;
        }
        
        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (proxy == null) return;

            var curParent = proxy.transform.parent ?? original.transform.parent;
            if (curParent != null && _finalBonesMap.TryGetValue(curParent, out var newRoot))
            {
                // We need to remember this proxy so we can avoid destroying it when we destroy VirtualAvatarRoot
                // in Dispose

                _knownProxies.Add(proxy.transform);

                proxy.transform.SetParent(newRoot, false);
            }

            var smr = proxy as SkinnedMeshRenderer;
            if (smr == null) return;

            var rootBone = _finalBonesMap.TryGetValue(smr.rootBone, out var newRootBone) ? newRootBone : smr.rootBone;
            smr.rootBone = rootBone;
            smr.bones = smr.bones.Select(b => b == null ? null : _finalBonesMap.GetValueOrDefault(b, b)).ToArray();
        }
        
        public void Dispose()
        {
            foreach (var proxy in _knownProxies)
            {
                if (proxy != null && proxy.IsChildOf(VirtualAvatarRoot.transform))
                {
                    proxy.transform.SetParent(null, false);
                }
            }
            
            Object.DestroyImmediate(VirtualAvatarRoot);

            _srcBones.Dispose();
            _dstBones.Dispose();
            _boneIsValid.Dispose();
            _boneStates.Dispose();
        }
    }
}
