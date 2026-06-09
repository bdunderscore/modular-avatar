using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    public class MeshDeleterPreview : IRenderFilter
    {
        private static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Mesh Deleter",
            qualifiedName: "nadena.dev.modular-avatar/MeshDeleterPreview",
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

        private readonly PropCache<SkinnedMeshRenderer, ImmutableList<IMeshSelector>> _cache = new(
            "FiltersForRenderer", FiltersForRenderer, Enumerable.SequenceEqual);

        private static ImmutableList<IMeshSelector> FiltersForRenderer(ComputeContext context, Renderer renderer)
        {
            if (renderer == null)
            {
                return ImmutableList<IMeshSelector>.Empty;
            }

            var avatar = context.GetAvatarRoot(renderer.gameObject);
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatar);
            var filters = ImmutableList<IMeshSelector>.Empty;

            foreach (var property in analysis.Shapes.Values)
            {
                var prop = property.TargetProp;
                if (prop.TargetObject != renderer) continue;
                if (prop.TargetObject is not SkinnedMeshRenderer smr || smr.sharedMesh == null) continue;
                if (!property.actionGroups.Any(x => x.Value is IMeshSelector)) continue;

                var activeRule = property.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not IMeshSelector filter) continue;

                filters = filters.Add(filter);
            }

            return filters;
        }

        private IEnumerable<RenderGroup> GroupsForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);
            var renderers = new HashSet<Renderer>();

            foreach (var property in analysis.Shapes.Values)
            {
                var prop = property.TargetProp;
                if (prop.TargetObject is not SkinnedMeshRenderer smr || smr.sharedMesh == null) continue;
                if (!property.actionGroups.Any(x => x.Value is IMeshSelector)) continue;

                var activeRule = property.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not IMeshSelector) continue;

                renderers.Add(smr);
            }

            return renderers.Select(RenderGroup.For);
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var avatarRoots = context.GetAvatarRoots();
            return avatarRoots.SelectMany(r => GroupsForAvatar(context, r)).ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var (original, proxy) = proxyPairs.First();
            var node = new Node(_cache, original as SkinnedMeshRenderer, proxy as SkinnedMeshRenderer, context);
            return node.Refresh(null, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            private readonly PropCache<SkinnedMeshRenderer, ImmutableList<IMeshSelector>> _cache;
            private readonly SkinnedMeshRenderer _original;
            private readonly ImmutableList<IMeshSelector> _filters;
            private Mesh _generatedMesh;

            public RenderAspects WhatChanged => RenderAspects.Mesh;

            internal Node(PropCache<SkinnedMeshRenderer, ImmutableList<IMeshSelector>> cache,
                SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, ComputeContext context)
            {
                _cache = cache;
                _original = original;
                _filters = _cache.Get(context, _original);
                _generatedMesh = GenerateMesh(proxy, proxy.sharedMesh, _filters);

                foreach (var filter in _filters)
                {
                    filter.Observe(context);
                }
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                if ((updatedAspects & RenderAspects.Mesh) != 0)
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                var filters = _cache.Get(context, _original);
                if (!filters.SequenceEqual(_filters))
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (original is not SkinnedMeshRenderer) return;
                if (proxy is not SkinnedMeshRenderer smr || smr.sharedMesh == null) return;

                if (_generatedMesh != null)
                {
                    smr.sharedMesh = _generatedMesh;
                }
            }

            private static Mesh GenerateMesh(Renderer proxy, Mesh mesh, ImmutableList<IMeshSelector> filters)
            {
                if (mesh == null) return null;

                Profiler.BeginSample("FilterPrimitivesOnly");
                try
                {
                    return RemoveVerticesFromMesh.FilterPrimitivesOnly(proxy, mesh, filters);
                }
                finally
                {
                    Profiler.EndSample();
                }
            }

            public void Dispose()
            {
                if (_generatedMesh != null)
                {
                    Object.DestroyImmediate(_generatedMesh);
                    _generatedMesh = null;
                }
            }
        }
    }
}
