using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IVertexFilter<in T>
    {
        void Filter(T target, bool[] vertexMask);
    }

    internal class VertexFilterByShape : IVertexFilter<(string, float)>
    {
        private readonly Mesh _mesh;
        private readonly Vector3[] _deltaPositions;

        public VertexFilterByShape(Mesh mesh)
        {
            _mesh = mesh;
            _deltaPositions = new Vector3[mesh.vertexCount];
        }

        public void Filter((string, float) target, bool[] vertexMask)
        {
            var (shapeName, threshold) = target;

            var shapeIndex = _mesh.GetBlendShapeIndex(shapeName);
            if (shapeIndex < 0) return; // shape not found

            var sqrThreshold = threshold * threshold;
            for (var f = 0; f < _mesh.GetBlendShapeFrameCount(shapeIndex); f++)
            {
                _mesh.GetBlendShapeFrameVertices(shapeIndex, f, _deltaPositions, null, null);
                for (var v = 0; v < _deltaPositions.Length; v++)
                {
                    if (_deltaPositions[v].sqrMagnitude > sqrThreshold)
                    {
                        vertexMask[v] = true;
                    }
                }
            }
        }
    }

    internal class VertexFilterByMask : IVertexFilter<(int, Texture2D, MeshDeleteMode)>
    {
        private readonly Vector2[] _uv;
        private readonly HashSet<int>[] _subMeshIndices;

        public VertexFilterByMask(Mesh mesh)
        {
            _uv = mesh.uv;
            _subMeshIndices = Enumerable.Range(0, mesh.subMeshCount)
                .Select(x => mesh.GetIndices(x).ToHashSet())
                .ToArray();
        }

        public void Filter((int, Texture2D, MeshDeleteMode) target, bool[] vertexMask)
        {
            var (materialIndex, maskTexture, deleteMode) = target;

            foreach (var v in _subMeshIndices[Mathf.Min(materialIndex, _subMeshIndices.Length - 1)])
            {
                Color? deleteColor = deleteMode switch
                {
                    MeshDeleteMode.DeleteBlack => Color.black,
                    MeshDeleteMode.DeleteWhite => Color.white,
                    _ => null,
                };
                if (maskTexture.GetPixel((int)(maskTexture.width * _uv[v].x), (int)(maskTexture.height * _uv[v].y)) == deleteColor)
                {
                    vertexMask[v] = true;
                }
            }
        }
    }
}
