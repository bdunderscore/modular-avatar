#if MA_VRCSDK3_AVATARS
#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

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

        private class StaticContext
        {
            public StaticContext(GameObject avatarRoot, IEnumerable<(int, float)> shapes)
            {
                AvatarRoot = avatarRoot;
                Shapes = shapes.ToImmutableDictionary(kv => kv.Item1, kv => kv.Item2);
            }
         
            public GameObject AvatarRoot { get; }
            public ImmutableDictionary<int, float> Shapes { get; }
            
            public override bool Equals(object obj)
            {
                return obj is StaticContext other && Shapes.SequenceEqual(other.Shapes) && AvatarRoot == other.AvatarRoot;
            }
            
            public override int GetHashCode()
            {
                int hash = AvatarRoot.GetHashCode();
                foreach (var shape in Shapes)
                {
                    hash = hash * 31 + shape.GetHashCode();
                }
                
                return hash;
            }
        }
        
        private readonly PropCache<GameObject, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, ShapeInfo)>>>
            _blendshapeCache = new("ShapesForAvatar", ShapesForAvatar);
        
        private static ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, ShapeInfo)>> ShapesForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            if (avatarRoot == null || !context.ActiveInHierarchy(avatarRoot))
            {
                return ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, ShapeInfo)>>.Empty;
            }

            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);
            var shapes = analysis.Shapes;

            var rendererStates = ImmutableDictionary.CreateBuilder<SkinnedMeshRenderer, ImmutableDictionary<int, ShapeInfo>>();
            var avatarRootTransform = avatarRoot.transform;
            
            foreach (var prop in shapes.Values)
            {
                var target = prop.TargetProp;
                if (target.TargetObject == null || target.TargetObject is not SkinnedMeshRenderer r) continue;
                if (!r.transform.IsChildOf(avatarRootTransform)) continue;

                var isDelete = false;
                string shapeName = null;
                if (target.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedShapePrefix))
                {
                    isDelete = true;
                    shapeName = target.PropertyName.Substring(ReactiveObjectAnalyzer.DeletedShapePrefix.Length);
                }
                else if (target.PropertyName.StartsWith(ReactiveObjectAnalyzer.BlendshapePrefix))
                {
                    shapeName = target.PropertyName.Substring(ReactiveObjectAnalyzer.BlendshapePrefix.Length);
                }
                else
                {
                    continue;
                }
                
                var mesh = r.sharedMesh;
                if (mesh == null) continue;
                
                if (!rendererStates.TryGetValue(r, out var states))
                {
                    states = ImmutableDictionary<int, ShapeInfo>.Empty;
                    rendererStates[r] = states;
                }
                
                var index = r.sharedMesh.GetBlendShapeIndex(shapeName);
                if (index < 0) continue;

                var activeRule = prop.actionGroups.LastOrDefault(rule => rule.InitiallyActive);
                if (activeRule == null || activeRule.Value is not float value) continue;
                if (activeRule.ControllingObject == null) continue; // default value is being inherited
                
                if (isDelete)
                {
                    if (activeRule.Value is not float threshold) continue;
                    var info = ShapeInfo.SetDeletedShape(threshold);
                    states = states.SetItem(index, info);
                }
                else
                {
                    if (states.ContainsKey(index))
                    {
                        // Delete takes precedence over set in preview
                        continue;
                    }

                    value = Math.Clamp(value, 0, 100);
                    var info = ShapeInfo.SetBlendshapeValue(value);
                    if (!states.ContainsKey(index))
                    {
                        states = states.Add(index, info);
                    }
                }

                rendererStates[r] = states;
            }

            return rendererStates.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(shapePair => (shapePair.Key, shapePair.Value)
                ).ToImmutableList());
        }
        
        private IEnumerable<RenderGroup> ShapesToGroups(GameObject avatarRoot, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, ShapeInfo)>> shapes)
        {
            return shapes.Select(kv => RenderGroup.For(kv.Key).WithData(
                new StaticContext(avatarRoot, kv.Value.Where(shape => shape.Item2.deleteThreshold.HasValue)
                    .Select(shape => (shape.Item1, shape.Item2.deleteThreshold.Value)))
            ));
        }
        
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var roots = context.GetAvatarRoots();
            
            
            return roots
                .SelectMany(av =>
                    ShapesToGroups(av, _blendshapeCache.Get(context, av))
                )
                .ToImmutableList();
        }

        public async Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var shapeValues = group.GetData<StaticContext>();
            var node = new Node(shapeValues, proxyPairs.First().Item2 as SkinnedMeshRenderer, _blendshapeCache);

            var rv = await node.Refresh(proxyPairs, context, 0);
            if (rv == null)
            {
                context.Invalidate();
            }

            return node;
        }

        private class ShapeInfo
        {
            public float blendshapeValue { get; private set; }
            public float? deleteThreshold { get; private set; }
            
            public static ShapeInfo SetBlendshapeValue(float value)
            {
                return new ShapeInfo { blendshapeValue = value, deleteThreshold = null };
            }
            
            public static ShapeInfo SetDeletedShape(float threshold)
            {
                return new ShapeInfo { blendshapeValue = -1, deleteThreshold = threshold };
            }

            public override bool Equals(object obj)
            {
                return obj is ShapeInfo other &&
                       blendshapeValue == other.blendshapeValue &&
                       deleteThreshold == other.deleteThreshold;
            }
            
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + blendshapeValue.GetHashCode();
                    hash = hash * 23 + (deleteThreshold?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }
        
        private class Node : IRenderFilterNode
        {
            private readonly PropCache<GameObject, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, ShapeInfo)>>> _blendshapeCache;
            private readonly GameObject _avatarRoot;
            private ImmutableList<(int, ShapeInfo)> _shapes;
            private ImmutableDictionary<int, float> _toDelete;
            private Mesh _generatedMesh = null;

            public RenderAspects WhatChanged => RenderAspects.Shapes | RenderAspects.Mesh;

            internal Node(StaticContext staticContext, SkinnedMeshRenderer proxySmr, PropCache<GameObject, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, ShapeInfo)>>> blendshapeCache)
            {
                _blendshapeCache = blendshapeCache;
                _avatarRoot = staticContext.AvatarRoot;
                _toDelete = staticContext.Shapes;
                _shapes = ImmutableList<(int, ShapeInfo)>.Empty;
                _generatedMesh = GetGeneratedMesh(proxySmr, _toDelete);
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                if ((updatedAspects & RenderAspects.Mesh) != 0)
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                var avatarInfo = _blendshapeCache.Get(context, _avatarRoot);
                if (!avatarInfo.TryGetValue((SkinnedMeshRenderer)proxyPairs.First().Item1, out var shapes))
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                var toDelete = shapes.Where(shape => shape.Item2.deleteThreshold.HasValue)
                    .Select(shape => (shape.Item1, shape.Item2.deleteThreshold.Value)).ToImmutableDictionary(
                        p => p.Item1,
                        p => p.Item2
                    );
                if (!_toDelete.OrderBy(kv => kv.Key).SequenceEqual(toDelete.OrderBy(kv => kv.Key)))
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }
                
                _shapes = shapes;
                    
                return Task.FromResult<IRenderFilterNode>(this);
            }

            public Mesh GetGeneratedMesh(SkinnedMeshRenderer proxy, ImmutableDictionary<int, float> toDelete)
            {
                var mesh = proxy.sharedMesh;

                if (toDelete.Count > 0)
                {
                    mesh = Object.Instantiate(mesh);

                    var vertexFilter = new VertexFilterByShape(mesh);
                    bool[] targetVertex = new bool[mesh.vertexCount];
                    foreach (var bs in toDelete)
                    {
                        vertexFilter.Filter((mesh.GetBlendShapeName(bs.Key), bs.Value), targetVertex);
                    }

                    List<int> tris = new List<int>();
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    {
                        tris.Clear();

                        var baseVertex = (int)mesh.GetBaseVertex(subMesh);
                        mesh.GetTriangles(tris, subMesh, false);

                        for (int i = 0; i < tris.Count; i += 3)
                        {
                            if (targetVertex[tris[i] + baseVertex] || targetVertex[tris[i + 1] + baseVertex] ||
                                targetVertex[tris[i + 2] + baseVertex])
                            {
                                tris.RemoveRange(i, 3);
                                i -= 3;
                            }
                        }

                        mesh.SetTriangles(tris, subMesh, false, baseVertex: baseVertex);
                    }

                    return mesh;
                }

                return null;
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (original == null || proxy == null) return;
                if (original is not SkinnedMeshRenderer originalSmr || proxy is not SkinnedMeshRenderer proxySmr) return;

                if (_generatedMesh != null)
                {
                    proxySmr.sharedMesh = _generatedMesh;
                }

                foreach (var shape in _shapes)
                {
                    proxySmr.SetBlendShapeWeight(shape.Item1, shape.Item2.deleteThreshold.HasValue ? 0 : shape.Item2.blendshapeValue);
                }
            }

            public void Dispose()
            {
                if (_generatedMesh != null) Object.DestroyImmediate(_generatedMesh);
            }
        }
    }
}
#endif