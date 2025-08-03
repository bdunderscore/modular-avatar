using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByAxisComponent))]
    internal sealed class VertexFilterByAxis : IVertexFilter
    {
        private readonly Vector3 _axis;

        public VertexFilterByAxis(VertexFilterByAxisComponent component, ComputeContext context)
        {
            _axis = context.Observe(component, c => c.Axis).normalized;
        }

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByAxis
                   && ((VertexFilterByAxis)other)._axis == _axis;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(typeof(VertexFilterByAxis), _axis);
        }

        public override string ToString()
        {
            return $"VertexFilterByAxis: {_axis}";
        }

        public void MarkFilteredVertices(Transform referenceSpace, Mesh mesh, bool[] filtered)
        {
            var meshSpaceAxis = referenceSpace.InverseTransformDirection(_axis);

            var vertices = mesh.vertices;

            if (vertices.Length != filtered.Length)
                throw new ArgumentException("Mesh vertex count does not match filtered array length.");

            for (var i = 0; i < vertices.Length; i++)
            {
                if (Vector3.Dot(meshSpaceAxis, vertices[i]) <= 0.0f)
                {
                    filtered[i] = false;
                }
            }
        }
    }
}