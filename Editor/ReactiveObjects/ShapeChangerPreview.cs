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

                var target = ctx.Observe(changer, _ => changer.targetRenderer.Get(changer));
                var renderer = ctx.GetComponent<SkinnedMeshRenderer>(target);

                if (renderer == null) continue;

                if (!groups.TryGetValue(renderer, out var group))
                {
                    group = ImmutableList.CreateBuilder<ModularAvatarShapeChanger>();
                    groups[renderer] = group;
                }

                group.Add(changer);
            }
            
            return groups.Select(g => RenderGroup.For(g.Key).WithData(g.Value.ToImmutable()))
                .ToImmutableList();
        }

        public async Task<IRenderFilterNode> Instantiate(
            RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            var node = new Node(group);

            try
            {
                await node.Init(proxyPairs, context);
            }
            catch (Exception e)
            {
                // dispose
                throw;
            }

            return node;
        }

        private class Node : IRenderFilterNode
        {
            private readonly RenderGroup _group;
            
            private Mesh _generatedMesh = null;
            private ImmutableList<ModularAvatarShapeChanger> _changers;
            private HashSet<int> _toDelete;

            internal Node(RenderGroup group)
            {
                _group = group;
            }

            private HashSet<int> GetToDeleteSet(SkinnedMeshRenderer proxy, ComputeContext context)
            {
                _changers = _group.GetData<ImmutableList<ModularAvatarShapeChanger>>();

                var toDelete = new HashSet<int>();
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
                    var shapes = context.Observe(changer, c => c.Shapes.ToImmutableList(), Enumerable.SequenceEqual);
                    
                    foreach (var shape in shapes)
                        if (shape.ChangeType == ShapeChangeType.Delete)
                        {
                            var index = mesh.GetBlendShapeIndex(shape.ShapeName);
                            if (index < 0) continue;
                            toDelete.Add(index);
                        }
                }

                return toDelete;
            }

            public async Task Init(
                IEnumerable<(Renderer, Renderer)> renderers,
                ComputeContext context
            )
            {
                var (original, proxy) = renderers.First();

                if (original == null || proxy == null) return;
                if (!(proxy is SkinnedMeshRenderer smr)) return;

                await Init(smr, context, GetToDeleteSet(smr, context));
            }

            public async Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs,
                ComputeContext context, RenderAspects updatedAspects)
            {
                if ((updatedAspects & RenderAspects.Mesh) != 0) return null;

                var (original, proxy) = proxyPairs.First();

                if (original == null || proxy == null) return null;
                if (!(proxy is SkinnedMeshRenderer smr)) return null;

                var toDelete = GetToDeleteSet(smr, context);
                if (toDelete.Count == _toDelete.Count && toDelete.All(_toDelete.Contains))
                {
                    return this;
                }

                var node = new Node(_group);
                await node.Init(smr, context, toDelete);
                return node;
            }

            public async Task Init(
                SkinnedMeshRenderer proxy,
                ComputeContext context,
                HashSet<int> toDelete
            )
            {
                _toDelete = toDelete;
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
                    
                    _generatedMesh = mesh;
                }
            }


            public RenderAspects Reads => RenderAspects.Shapes | RenderAspects.Mesh;
            public RenderAspects WhatChanged => RenderAspects.Shapes | RenderAspects.Mesh;

            public void Dispose()
            {
                if (_generatedMesh != null) Object.DestroyImmediate(_generatedMesh);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (_changers == null) return; // can happen transiently as we disable the last component
                if (!(proxy is SkinnedMeshRenderer smr)) return;

                Mesh mesh;
                if (_generatedMesh != null)
                {
                    smr.sharedMesh = _generatedMesh;
                    mesh = _generatedMesh;
                }
                else
                {
                    mesh = smr.sharedMesh;
                }

                if (mesh == null) return;

                foreach (var changer in _changers)
                {
                    foreach (var shape in changer.Shapes)
                    {
                        var index = mesh.GetBlendShapeIndex(shape.ShapeName);
                        if (index < 0) continue;

                        float setToValue = -1;

                        switch (shape.ChangeType)
                        {
                            case ShapeChangeType.Delete:
                                setToValue = 100;
                                break;
                            case ShapeChangeType.Set:
                                setToValue = shape.Value;
                                break;
                        }

                        smr.SetBlendShapeWeight(index, setToValue);
                    }
                }
            }
        }
    }
}