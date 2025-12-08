using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    public class MeshDeformPreview : IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            return context.GetAvatarRoots()
                .SelectMany(r => r.GetComponentsInChildren<AbstractMeshDeformComponent>(false))
                .GroupBy(r => context.Observe((Component)r, _ => r.Target.Get((Component)r)))
                .Where(r => r.Key != null && context.GetComponent<SkinnedMeshRenderer>(r.Key) != null)
                .Select(r => RenderGroup.For(r.Key.GetComponent<Renderer>()).WithData(r.ToImmutableList()))
                .ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            var firstProxy = proxyPairs.First();
            return Task.FromResult<IRenderFilterNode>(new Node(
                group.GetData<ImmutableList<AbstractMeshDeformComponent>>(),
                firstProxy.Item1,
                firstProxy.Item2,
                context
            ));
        }

        private class Node : IRenderFilterNode
        {
            private const string ShapeName = "____ModularAvatarMeshDeform";
            private readonly ImmutableList<AbstractMeshDeformComponent> _filters;
            private readonly Renderer _original;
            private readonly Renderer _proxy;
            private readonly ComputeContext _context;

            private readonly Mesh mesh;

            private readonly List<int> blendshapeIndexes = new();

            public RenderAspects WhatChanged => RenderAspects.Mesh;

            public Node(ImmutableList<AbstractMeshDeformComponent> filters, Renderer original, Renderer proxy,
                ComputeContext context)
            {
                _filters = filters;
                _original = original;
                _proxy = proxy;
                _context = context;

                context.ObserveTransformPosition(original.transform);

                var smr = (SkinnedMeshRenderer)proxy;
                mesh = smr.sharedMesh;

                for (var i = 0; i < _filters.Count; i++)
                {
                    var filter = _filters[i];
                    context.ObserveTransformPosition(((Component)filter).transform);

                    var shapeName = ShapeName + "_" + i;
                    var deformer = MeshDeformDatabase.GetDeformer(context, filter);
                    MeshDeformProcessor.AddMeshDeform(ref mesh, proxy, shapeName, filter, deformer);
                    blendshapeIndexes.Add(mesh.GetBlendShapeIndex(shapeName));
                }
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                var smr = (SkinnedMeshRenderer)proxy;
                smr.sharedMesh = mesh;
                foreach (var index in blendshapeIndexes)
                {
                    smr.SetBlendShapeWeight(index, 100);
                }
            }
        }
    }
}