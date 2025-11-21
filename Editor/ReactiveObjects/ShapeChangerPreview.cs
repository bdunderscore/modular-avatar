using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public class ShapeChangerPreview : IRenderFilter
    {
        private static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Shape Changer",
            qualifiedName: "nadena.dev.modular-avatar/ShapeChangerPreview",
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

        private readonly PropCache<SkinnedMeshRenderer, ImmutableList<(int, float)>> _cache = new(
            "ShapesForRenderer", ShapesForRenderer, Enumerable.SequenceEqual);

        private static ImmutableList<(int, float)> ShapesForRenderer(ComputeContext context, Renderer renderer)
        {
            if (renderer == null)
            {
                return ImmutableList<(int, float)>.Empty;
            }

            var avatar = context.GetAvatarRoot(renderer.gameObject);
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatar);
            var shapes = ImmutableList<(int, float)>.Empty;

            foreach (var property in analysis.Shapes.Values)
            {
                var prop = property.TargetProp;
                if (prop.TargetObject != renderer) continue;
                if (prop.TargetObject is not SkinnedMeshRenderer smr || smr.sharedMesh == null) continue;
                if (!prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.BlendshapePrefix)) continue;

                var shapeName = prop.PropertyName[ReactiveObjectAnalyzer.BlendshapePrefix.Length..];
                var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                if (shapeIndex < 0) continue;

                var activeRule = property.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not float value) continue;

                shapes = shapes.Add((shapeIndex, Mathf.Clamp(value, 0, 100)));
            }

            return shapes;
        }

        private IEnumerable<RenderGroup> GroupsForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);
            var renderers = new HashSet<Renderer>();

            foreach (var property in analysis.Shapes.Values)
            {
                var prop = property.TargetProp;
                if (prop.TargetObject is not SkinnedMeshRenderer smr || smr.sharedMesh == null) continue;
                if (!prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.BlendshapePrefix)) continue;

                var shapeName = prop.PropertyName[ReactiveObjectAnalyzer.BlendshapePrefix.Length..];
                var shapeIndex = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                if (shapeIndex < 0) continue;

                var activeRule = property.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not float) continue;

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
            private readonly PropCache<SkinnedMeshRenderer, ImmutableList<(int, float)>> _cache;
            private readonly SkinnedMeshRenderer _original;
            private readonly ImmutableList<(int, float)> _shapes;

            public RenderAspects WhatChanged => RenderAspects.Shapes;

            internal Node(PropCache<SkinnedMeshRenderer, ImmutableList<(int, float)>> cache,
                SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, ComputeContext context)
            {
                _cache = cache;
                _original = original;
                _shapes = _cache.Get(context, _original);
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                if ((updatedAspects & RenderAspects.Shapes) != 0)
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                var shapes = _cache.Get(context, _original);
                if (!shapes.SequenceEqual(_shapes))
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (original is not SkinnedMeshRenderer) return;
                if (proxy is not SkinnedMeshRenderer smr || smr.sharedMesh == null) return;

                foreach (var shape in _shapes)
                {
                    smr.SetBlendShapeWeight(shape.Item1, shape.Item2);
                }
            }
        }
    }
}
