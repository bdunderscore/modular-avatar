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
            public StaticContext(GameObject avatarRoot, IEnumerable<int> shapes)
            {
                AvatarRoot = avatarRoot;
                Shapes = shapes.OrderBy(i => i).ToImmutableList();
            }
         
            public GameObject AvatarRoot { get; }
            public ImmutableList<int> Shapes { get; }
            
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
        
        private readonly PropCache<GameObject, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>>>
            _blendshapeCache = new("ShapesForAvatar", ShapesForAvatar);
        
        private static ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>> ShapesForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            if (avatarRoot == null || !context.ActiveInHierarchy(avatarRoot))
            {
                return ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>>.Empty;
            }

            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);
            var shapes = analysis.Shapes;

            ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>>.Builder rendererStates =
                ImmutableDictionary.CreateBuilder<SkinnedMeshRenderer, ImmutableList<(int, float)>>(
                    
                );
            var avatarRootTransform = avatarRoot.transform;
            
            foreach (var prop in shapes.Values)
            {
                var target = prop.TargetProp;
                if (target.TargetObject == null || target.TargetObject is not SkinnedMeshRenderer r) continue;
                if (!r.transform.IsChildOf(avatarRootTransform)) continue;
                if (!target.PropertyName.StartsWith("blendShape.")) continue;

                var mesh = r.sharedMesh;
                if (mesh == null) continue;
                
                var shapeName = target.PropertyName.Substring("blendShape.".Length);
                
                if (!rendererStates.TryGetValue(r, out var states))
                {
                    states = ImmutableList<(int, float)>.Empty;
                    rendererStates[r] = states;
                }
                
                var index = r.sharedMesh.GetBlendShapeIndex(shapeName);
                if (index < 0) continue;

                var activeRule = prop.actionGroups.LastOrDefault(rule => rule.InitiallyActive);
                if (activeRule == null || activeRule.Value is not float value) continue;

                value = Math.Clamp(value, 0, 100);
                
                if (activeRule.IsDelete) value = -1;
                
                states = states.Add((index, value));
                rendererStates[r] = states;
            }
            
            return rendererStates.ToImmutableDictionary();
        }
        
        private IEnumerable<RenderGroup> ShapesToGroups(GameObject avatarRoot, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>> shapes)
        {
            return shapes.Select(kv => RenderGroup.For(kv.Key).WithData(
                new StaticContext(avatarRoot, kv.Value.Where(shape => shape.Item2 < 0).Select(shape => shape.Item1))
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

        private class Node : IRenderFilterNode
        {
            private readonly PropCache<GameObject, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>>> _blendshapeCache;
            private readonly GameObject _avatarRoot;
            private ImmutableList<(int, float)> _shapes;
            private ImmutableHashSet<int> _toDelete;
            private Mesh _generatedMesh = null;

            public RenderAspects WhatChanged => RenderAspects.Shapes | RenderAspects.Mesh;

            internal Node(StaticContext staticContext, SkinnedMeshRenderer proxySmr, PropCache<GameObject, ImmutableDictionary<SkinnedMeshRenderer, ImmutableList<(int, float)>>> blendshapeCache)
            {
                _blendshapeCache = blendshapeCache;
                _avatarRoot = staticContext.AvatarRoot;
                _toDelete = staticContext.Shapes.ToImmutableHashSet();
                _shapes = ImmutableList<(int, float)>.Empty;
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

                var toDelete = shapes.Where(shape => shape.Item2 < 0).Select(shape => shape.Item1).ToImmutableHashSet();
                if (!_toDelete.SetEquals(toDelete))
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }
                
                _shapes = shapes;
                    
                return Task.FromResult<IRenderFilterNode>(this);
            }

            public Mesh GetGeneratedMesh(SkinnedMeshRenderer proxy, ImmutableHashSet<int> toDelete)
            {
                var mesh = proxy.sharedMesh;

                if (toDelete.Count > 0)
                {
                    mesh = Object.Instantiate(mesh);

                    var bsPos = new Vector3[mesh.vertexCount];
                    bool[] targetVertex = new bool[mesh.vertexCount];
                    foreach (var bs in toDelete)
                    {
                        int frames = mesh.GetBlendShapeFrameCount(bs);
                        for (int f = 0; f < frames; f++)
                        {
                            mesh.GetBlendShapeFrameVertices(bs, f, bsPos, null, null);

                            for (int i = 0; i < bsPos.Length; i++)
                            {
                                if (bsPos[i].sqrMagnitude > 0.0001f)
                                {
                                    targetVertex[i] = true;
                                }
                            }
                        }
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
                    proxySmr.SetBlendShapeWeight(shape.Item1, shape.Item2 < 0 ? 100 : shape.Item2);
                }
            }

            public void Dispose()
            {
                if (_generatedMesh != null) Object.DestroyImmediate(_generatedMesh);
            }
        }
    }
}