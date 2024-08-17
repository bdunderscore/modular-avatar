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
        
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext ctx)
        {
            var menuItemPreview = new MenuItemPreviewCondition(ctx);
            
            var allChangers = ctx.GetComponentsByType<ModularAvatarShapeChanger>();

            var groups =
                new Dictionary<Renderer, ImmutableList<ModularAvatarShapeChanger>.Builder>(
                    new ObjectIdentityComparer<Renderer>());

            foreach (var changer in allChangers)
            {
                if (changer == null) continue;

                var mami = ctx.GetComponent<ModularAvatarMenuItem>(changer.gameObject);
                bool active = ctx.ActiveAndEnabled(changer) && (mami == null || menuItemPreview.IsEnabledForPreview(mami));
                if (active == ctx.Observe(changer, t => t.Inverted)) continue;

                var overrideTarget = ctx.Observe(changer, c => c.targetRenderer.Get(changer));
                var overrideRenderer = ctx.GetComponent<SkinnedMeshRenderer>(overrideTarget);

                var shapes = ctx.Observe(changer, c => c.Shapes.Select(s => (s.Object.Get(c), s.ShapeName, s.ChangeType, s.Value)).ToList(), Enumerable.SequenceEqual);

                foreach (var (target, name, type, value) in shapes)
                {
                    var renderer = overrideRenderer ?? ctx.GetComponent<SkinnedMeshRenderer>(target);
                    if (renderer == null) continue;

                    if (!groups.TryGetValue(renderer, out var group))
                    {
                        group = ImmutableList.CreateBuilder<ModularAvatarShapeChanger>();
                        groups[renderer] = group;
                    }

                    group.Add(changer);
                }
            }
            
            return groups.Select(g => RenderGroup.For(g.Key).WithData(g.Value.ToImmutable()))
                .ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var changers = group.GetData<ImmutableList<ModularAvatarShapeChanger>>();
            var node = new Node(changers);

            return node.Refresh(proxyPairs, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            private readonly ImmutableList<ModularAvatarShapeChanger> _changers;
            private ImmutableHashSet<(int, float)> _shapes;
            private ImmutableHashSet<int> _toDelete;
            private Mesh _generatedMesh = null;

            internal Node(ImmutableList<ModularAvatarShapeChanger> changers)
            {
                _changers = changers;
                _shapes = ImmutableHashSet<(int, float)>.Empty;
                _toDelete = ImmutableHashSet<int>.Empty;
                _generatedMesh = null;
            }

            private ImmutableHashSet<(int, float)> GetShapesSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, ComputeContext context)
            {
                var builder = ImmutableHashSet.CreateBuilder<(int, float)>();
                var mesh = context.Observe(proxy, p => p.sharedMesh, (a, b) =>
                {
                    if (a != b)
                    {
                        Debug.Log($"mesh changed {a.GetInstanceID()} -> {b.GetInstanceID()}");
                        return false;
                    }

                    return true;
                });

                foreach (var changer in _changers)
                {
                    var overrideTarget = context.Observe(changer, c => c.targetRenderer.Get(changer));
                    var overrideRenderer = context.GetComponent<SkinnedMeshRenderer>(overrideTarget);

                    var shapes = context.Observe(changer, c => c.Shapes.Select(s => (s.Object.Get(c), s.ShapeName, s.ChangeType, s.Value)).ToList(), Enumerable.SequenceEqual);

                    foreach (var (target, name, type, value) in shapes)
                    {
                        var renderer = overrideRenderer ?? context.GetComponent<SkinnedMeshRenderer>(target);
                        if (renderer != original) continue;

                        var index = mesh.GetBlendShapeIndex(name);
                        if (index < 0) continue;
                        builder.Add((index, type == ShapeChangeType.Delete ? 100 : value));
                    }
                }

                return builder.ToImmutable();
            }

            private ImmutableHashSet<int> GetToDeleteSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, ComputeContext context)
            {
                var builder = ImmutableHashSet.CreateBuilder<int>();
                var mesh = context.Observe(proxy, p => p.sharedMesh, (a, b) =>
                {
                    if (a != b)
                    {
                        Debug.Log($"mesh changed {a.GetInstanceID()} -> {b.GetInstanceID()}");
                        return false;
                    }

                    return true;
                });

                foreach (var changer in _changers)
                {
                    var overrideTarget = context.Observe(changer, c => c.targetRenderer.Get(changer));
                    var overrideRenderer = context.GetComponent<SkinnedMeshRenderer>(overrideTarget);

                    var shapes = context.Observe(changer, c => c.Shapes.Select(s => (s.Object.Get(c), s.ShapeName, s.ChangeType, s.Value)).ToList(), Enumerable.SequenceEqual);

                    foreach (var (target, name, type, value) in shapes)
                    {
                        if (type != ShapeChangeType.Delete) continue;

                        var renderer = overrideRenderer ?? context.GetComponent<SkinnedMeshRenderer>(target);
                        if (renderer != original) continue;

                        var index = mesh.GetBlendShapeIndex(name);
                        if (index < 0) continue;
                        builder.Add(index);
                    }
                }

                return builder.ToImmutable();
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                var (original, proxy) = proxyPairs.First();

                if (original == null || proxy == null) return null;
                if (original is not SkinnedMeshRenderer originalSmr || proxy is not SkinnedMeshRenderer proxySmr) return null;

                var shapes = GetShapesSet(originalSmr, proxySmr, context);
                var toDelete = GetToDeleteSet(originalSmr, proxySmr, context);

                if (!toDelete.SequenceEqual(_toDelete))
                {
                    return Task.FromResult<IRenderFilterNode>(new Node(_changers)
                    {
                        _shapes = shapes,
                        _toDelete = toDelete,
                        _generatedMesh = GetGeneratedMesh(proxySmr, toDelete),
                    });
                }

                if (!shapes.SequenceEqual(_shapes))
                {
                    var reusableMesh = _generatedMesh;
                    _generatedMesh = null;
                    return Task.FromResult<IRenderFilterNode>(new Node(_changers)
                    {
                        _shapes = shapes,
                        _toDelete = toDelete,
                        _generatedMesh = reusableMesh,
                    });
                }

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


            public RenderAspects Reads => RenderAspects.Shapes | RenderAspects.Mesh;
            public RenderAspects WhatChanged => RenderAspects.Shapes | RenderAspects.Mesh;

            public void Dispose()
            {
                if (_generatedMesh != null) Object.DestroyImmediate(_generatedMesh);
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
                    proxySmr.SetBlendShapeWeight(shape.Item1, shape.Item2);
                }
            }
        }
    }
}