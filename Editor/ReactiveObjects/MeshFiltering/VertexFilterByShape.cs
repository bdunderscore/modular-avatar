using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByShapeComponent))]
    internal class VertexFilterByShape : IVertexFilter
    {
        public ImmutableHashSet<string> Shapes { get; }

        public float Threshold { get; }

        public VertexFilterByShape(string shapeName, float threshold)
        {
            Shapes = ImmutableHashSet<string>.Empty.Add(shapeName);
            Threshold = threshold;
        }

        public VertexFilterByShape(IEnumerable<string> shapes, float threshold)
        {
            Shapes = shapes.ToImmutableHashSet();
            Threshold = threshold;
        }

        public VertexFilterByShape(VertexFilterByShapeComponent component, ComputeContext context)
        {
            (Shapes, Threshold) = context.Observe(
                component,
                c => (c.Shapes.ToImmutableHashSet(), c.Threshold),
                (a, b) => { return a.Item1.SetEquals(b.Item1) && Mathf.Approximately(a.Item2, b.Item2); }
            );
        }

        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            var deltaPositions = new Vector3[mesh.vertexCount];

            foreach (var shape in Shapes)
            {
                var shapeIndex = mesh.GetBlendShapeIndex(shape);
                if (shapeIndex < 0) continue; // shape not found

                var sqrThreshold = Threshold * Threshold;

                for (var f = 0; f < mesh.GetBlendShapeFrameCount(shapeIndex); f++)
                {
                    mesh.GetBlendShapeFrameVertices(shapeIndex, f, deltaPositions, null, null);
                    for (var v = 0; v < deltaPositions.Length; v++)
                    {
                        if (deltaPositions[v].sqrMagnitude > sqrThreshold)
                        {
                            filtered[v] = true;
                        }
                    }
                }
            }
        }

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByShape filter
                   && filter.Shapes.SetEquals(Shapes)
                   && Mathf.Approximately(filter.Threshold, Threshold);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IVertexFilter);
        }

        public override int GetHashCode()
        {
            var hashCode = Threshold.GetHashCode();
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