using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class VertexFilterByShape : IVertexFilter
    {
        public string ShapeName { get; }

        public float Threshold { get; }

        public VertexFilterByShape(string shapeName, float threshold)
        {
            ShapeName = shapeName;
            Threshold = threshold;
        }

        public void MarkFilteredVertices(Transform referenceSpace, Mesh mesh, bool[] filtered)
        {
            var shapeIndex = mesh.GetBlendShapeIndex(ShapeName);
            if (shapeIndex < 0) return; // shape not found

            var deltaPositions = new Vector3[mesh.vertexCount];
            var sqrThreshold = Threshold * Threshold;

            for (var f = 0; f < mesh.GetBlendShapeFrameCount(shapeIndex); f++)
            {
                mesh.GetBlendShapeFrameVertices(shapeIndex, f, deltaPositions, null, null);
                for (var v = 0; v < deltaPositions.Length; v++)
                {
                    if (deltaPositions[v].sqrMagnitude <= sqrThreshold)
                    {
                        filtered[v] = false;
                    }
                }
            }
        }

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByShape filter
                   && filter.ShapeName == ShapeName
                   && filter.Threshold == Threshold;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IVertexFilter);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ShapeName, Threshold);
        }

        public override string ToString()
        {
            return $"{ShapeName}_{Threshold}";
        }
    }
}