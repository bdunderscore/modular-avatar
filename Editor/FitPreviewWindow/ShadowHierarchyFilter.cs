#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal class ShadowHierarchyFilter : IRenderFilter, IDisposable
    {
        /*
         * Q: When to add bones?
         *   - IRenderFilterNode instantiation time
         * Q: when to remove bones? -> Dispose
         * Sync method? Use ArmatureLock
         */

        public PublishedValue<GameObject?> targetAvatarRoot = new(null);

        private readonly Scene _scene;
        internal ShadowBoneHierarchy? _shadowBoneHierarchy;

        public ShadowHierarchyFilter(Scene scene)
        {
            _scene = scene;
        }

        public void Dispose()
        {
            _shadowBoneHierarchy?.Dispose();
        }
        
        private ShadowBoneHierarchy GetHierarchy(GameObject root)
        {
            if (_shadowBoneHierarchy?.IsValid == true && _shadowBoneHierarchy?.Root == root) return _shadowBoneHierarchy;

            _shadowBoneHierarchy?.Dispose();
            _shadowBoneHierarchy = new ShadowBoneHierarchy(root, _scene);
            
            return _shadowBoneHierarchy;
        }

        private ShadowBoneHierarchy GetProxySceneHierarchy(GameObject root)
        {
            GetHierarchy(root); // ensure we've created the right hierarchy
            return _shadowBoneHierarchy!;
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var root = context.Observe(targetAvatarRoot, r => r, (a, b) => a == b);
            if (root == null) return ImmutableList<RenderGroup>.Empty;

            // NDMF only supports Mesh and SkinnedMeshRenderers currently
            var renderers = context.GetComponentsInChildren<Renderer>(root, true)
                .Where(r => r is MeshRenderer or SkinnedMeshRenderer);
            return ImmutableList.Create(RenderGroup.For(renderers).WithData(root));
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            var shadowHierarchy = GetHierarchy(group.GetData<GameObject>());
            var proxyHierarchy = GetProxySceneHierarchy(group.GetData<GameObject>());
            Dictionary<Transform, Transform> boneRefs = new();
            List<(Renderer, Transform[])> boneArrayReplacements = new();

            // Original object -> Proxy
            Dictionary<Transform, Transform> meshRendererHosts = new();

            foreach (var (original, proxy) in proxyPairs)
            {
                if (proxy is not SkinnedMeshRenderer smr) continue;

                var origBones = smr.bones;
                var replacementBones = origBones.Select(b => TranslateBone(b)).ToArray();
                boneArrayReplacements.Add((original, replacementBones));
            }

            // We do mesh renderers second as we want to attach them to the hierarchy of the SMRs, which can
            // include e.g. bones manipulated by ScaleAdjuster
            foreach (var (original, proxy) in proxyPairs)
            {
                if (proxy is not SkinnedMeshRenderer)
                {
                    var rendererHostObject = TranslateBone(original.gameObject.transform)!;
                    var proxyHostObject = rendererHostObject;
                    meshRendererHosts.Add(original.gameObject.transform, proxyHostObject);
                    continue;
                }
            }

            return Task.FromResult<IRenderFilterNode>(
                new Node(boneRefs, boneArrayReplacements, shadowHierarchy, proxyHierarchy, meshRendererHosts)
            );

            Transform? TranslateBone(Transform? bone)
            {
                if (bone == null) return null;
                if (boneRefs.TryGetValue(bone, out var newBone)) return newBone;

                return shadowHierarchy.GetOrCreateTransform(bone);
            }
        }

        private class Node : IRenderFilterNode
        {
            private readonly ShadowBoneHierarchy _shadowBoneHierarchy, _proxyBoneHierarchy;
            private readonly Dictionary<Transform, Transform> _boneRefs;
            private readonly Dictionary<Renderer, Transform[]> _boneArrayReplacements;
            private readonly Dictionary<Transform, Transform> _meshRendererHosts;

            public Node(
                Dictionary<Transform, Transform> boneRefs,
                List<(Renderer, Transform[])> boneArrayReplacements,
                ShadowBoneHierarchy shadowBoneHierarchy,
                ShadowBoneHierarchy proxyBoneHierarchy,
                Dictionary<Transform, Transform> meshRendererHosts
            )
            {
                _boneRefs = boneRefs;
                _boneArrayReplacements = boneArrayReplacements.ToDictionary(e => e.Item1, e => e.Item2);
                _shadowBoneHierarchy = shadowBoneHierarchy;
                _proxyBoneHierarchy = proxyBoneHierarchy;
                _meshRendererHosts = meshRendererHosts;
            }

            public RenderAspects WhatChanged => 0;

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (proxy is SkinnedMeshRenderer smr
                    && _boneArrayReplacements.TryGetValue(original, out var replacements))
                {
                    smr.bones = replacements;
                }
                else if (proxy is MeshRenderer && _meshRendererHosts.TryGetValue(original.transform, out var host))
                {
                    if (proxy.gameObject.scene != host.gameObject.scene)
                    {
                        proxy.transform.SetParent(null);
                        SceneManager.MoveGameObjectToScene(proxy.gameObject, host.gameObject.scene);
                    }

                    proxy.transform.SetParent(host, false);
                    proxy.transform.localPosition = Vector3.zero;
                    proxy.transform.localRotation = Quaternion.identity;
                    proxy.transform.localScale = Vector3.one;
                }
            }

            public void OnFrameGroup()
            {
                _shadowBoneHierarchy.Update();
            }

            public void Dispose()
            {
                foreach (var bone in _boneRefs.Values)
                {
                    _shadowBoneHierarchy?.ReleaseTransform(bone);
                }

                foreach (var bone in _meshRendererHosts.Values)
                {
                    _shadowBoneHierarchy?.ReleaseTransform(bone);
                }
                
                _shadowBoneHierarchy?.Dispose();
            }
        }
    }
}