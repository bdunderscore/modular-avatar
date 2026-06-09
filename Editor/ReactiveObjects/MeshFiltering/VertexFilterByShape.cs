using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByShapeComponent))]
    internal class VertexFilterByShape : IMeshSelector
    {
        public ImmutableHashSet<string> Shapes { get; }

        public float Threshold { get; }

        private readonly VertexSelectionMode _selectionMode;

        public VertexFilterByShape(
            string shapeName,
            float threshold,
            VertexSelectionMode selectionMode = VertexSelectionMode.AnyVertex
        )
        {
            Shapes = ImmutableHashSet<string>.Empty.Add(shapeName);
            Threshold = threshold;
            _selectionMode = NormalizeSelectionMode(selectionMode);
        }

        public VertexFilterByShape(
            IEnumerable<string> shapes,
            float threshold,
            VertexSelectionMode selectionMode = VertexSelectionMode.AnyVertex
        )
        {
            Shapes = shapes.ToImmutableHashSet();
            Threshold = threshold;
            _selectionMode = NormalizeSelectionMode(selectionMode);
        }

        public VertexFilterByShape(VertexFilterByShapeComponent component, ComputeContext context)
        {
            (Shapes, Threshold, _selectionMode) = context.Observe(
                component,
                c => (c.Shapes.ToImmutableHashSet(), c.Threshold, NormalizeSelectionMode(c.SelectionMode)),
                (a, b) =>
                {
                    return a.Item1.SetEquals(b.Item1)
                           && Mathf.Approximately(a.Item2, b.Item2)
                           && a.Item3 == b.Item3;
                }
            );
        }

        private static VertexSelectionMode NormalizeSelectionMode(VertexSelectionMode selectionMode)
        {
            return selectionMode == VertexSelectionMode.Centroid ? VertexSelectionMode.AnyVertex : selectionMode;
        }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            return job.MarkPrimitivesFromVertexIndices(
                this,
                () => BuildVertexMask(job.OriginalMesh),
                _selectionMode,
                submesh,
                selectedPrimitives
            );
        }

        private (NativeArray<bool>, JobHandle) BuildVertexMask(Mesh mesh)
        {
            var vertexMask = new NativeArray<bool>(mesh.vertexCount, Allocator.TempJob);
            var deltaPositions = new Vector3[mesh.vertexCount];
            var sqrThreshold = Threshold * Threshold;

            foreach (var shape in Shapes)
            {
                var shapeIndex = mesh.GetBlendShapeIndex(shape);
                if (shapeIndex < 0) continue;

                for (var f = 0; f < mesh.GetBlendShapeFrameCount(shapeIndex); f++)
                {
                    mesh.GetBlendShapeFrameVertices(shapeIndex, f, deltaPositions, null, null);
                    for (var v = 0; v < deltaPositions.Length; v++)
                    {
                        if (deltaPositions[v].sqrMagnitude > sqrThreshold)
                        {
                            vertexMask[v] = true;
                        }
                    }
                }
            }

            return (vertexMask, default);
        }

        public bool Equals(IMeshSelector other)
        {
            return other is VertexFilterByShape filter
                   && filter.Shapes.SetEquals(Shapes)
                   && Mathf.Approximately(filter.Threshold, Threshold)
                   && filter._selectionMode == _selectionMode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IMeshSelector);
        }

        public override int GetHashCode()
        {
            var hashCode = HashCode.Combine(Threshold, _selectionMode);
            foreach (var shape in Shapes.OrderBy(x => x))
            {
                hashCode = HashCode.Combine(hashCode, shape);
            }

            return hashCode;
        }

        public override string ToString()
        {
            return $"VertexFilterByShape: {string.Join(", ", Shapes)} @ {Threshold}";
        }
    }
}
