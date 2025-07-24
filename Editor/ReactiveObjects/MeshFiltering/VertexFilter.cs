using System;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;
#if MA_MASK_TEXTURE_EDITOR
using MaskTextureEditor = net.nekobako.MaskTextureEditor.Editor;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IVertexFilter : IEquatable<IVertexFilter>
    {
        void MarkFilteredVertices(Mesh mesh, bool[] filtered);
        void Observe(ComputeContext context) {}
    }

    internal class VertexFilterByShape : IVertexFilter
    {
        private readonly string _shapeName;
        private readonly float _threshold;

        public string ShapeName => _shapeName;
        public float Threshold => _threshold;

        public VertexFilterByShape(string shapeName, float threshold)
        {
            _shapeName = shapeName;
            _threshold = threshold;
        }

        public void MarkFilteredVertices(Mesh mesh, bool[] filtered)
        {
            var shapeIndex = mesh.GetBlendShapeIndex(_shapeName);
            if (shapeIndex < 0) return; // shape not found

            var deltaPositions = new Vector3[mesh.vertexCount];
            var sqrThreshold = _threshold * _threshold;

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

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByShape filter
                   && filter._shapeName == _shapeName
                   && filter._threshold == _threshold;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IVertexFilter);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_shapeName, _threshold);
        }

        public override string ToString()
        {
            return $"{_shapeName}_{_threshold}";
        }
    }

    internal class VertexFilterByMask : IVertexFilter
    {
        private readonly int _materialIndex;
        private readonly Texture2D _maskTexture;
        private readonly Hash128 _maskTextureContentHash;
        private readonly DeleteMeshByMaskMode _deleteMode;

        public int MaterialIndex => _materialIndex;
        public Texture2D MaskTexture => _maskTexture;
        public DeleteMeshByMaskMode DeleteMode => _deleteMode;

        private Texture2D _editingTexture;
        private Hash128 _editingTextureContentHash;

        public VertexFilterByMask(int materialIndex, Texture2D maskTexture, DeleteMeshByMaskMode deleteMode)
        {
            _materialIndex = materialIndex;
            _maskTexture = maskTexture;
            _maskTextureContentHash = maskTexture.imageContentsHash;
            _deleteMode = deleteMode;
        }

        public void MarkFilteredVertices(Mesh mesh, bool[] filtered)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return; // uv not found

            var subMeshIndices = Enumerable.Range(0, mesh.subMeshCount)
                .Select(x => mesh.GetIndices(x).ToHashSet())
                .ToArray();

            foreach (var v in subMeshIndices[Mathf.Min(_materialIndex, subMeshIndices.Length - 1)])
            {
                var targetTexture = _editingTexture ?? _maskTexture;
                Color? deleteColor = _deleteMode switch
                {
                    DeleteMeshByMaskMode.DeleteBlack => Color.black,
                    DeleteMeshByMaskMode.DeleteWhite => Color.white,
                    _ => null,
                };
                if (targetTexture.GetPixel((int)(targetTexture.width * uv[v].x), (int)(targetTexture.height * uv[v].y)) == deleteColor)
                {
                    filtered[v] = true;
                }
            }
        }

#if MA_MASK_TEXTURE_EDITOR
        public void Observe(ComputeContext context)
        {
            _editingTexture = MaskTextureEditor.Window.ObserveTextureFor(context, _maskTexture, null, null,
                MaskTextureEditorOpener.MaskTextureEditorToken);
            _editingTextureContentHash = _editingTexture?.imageContentsHash ?? default;
        }
#endif

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByMask filter
                   && filter._materialIndex == _materialIndex
                   && filter._maskTexture == _maskTexture
                   && filter._maskTextureContentHash == _maskTextureContentHash
                   && filter._editingTexture == _editingTexture
                   && filter._editingTextureContentHash == _editingTextureContentHash
                   && filter._deleteMode == _deleteMode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IVertexFilter);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                _materialIndex,
                _maskTexture, _maskTextureContentHash,
                _editingTexture, _editingTextureContentHash,
                _deleteMode);
        }

        public override string ToString()
        {
            return $"{_materialIndex}_{_maskTexture.GetInstanceID()}_{_deleteMode}";
        }
    }
}
