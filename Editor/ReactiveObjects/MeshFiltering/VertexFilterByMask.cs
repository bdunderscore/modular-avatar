using System;
using System.Linq;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEngine;
using Object = UnityEngine.Object;
#if MA_MASK_TEXTURE_EDITOR
using net.nekobako.MaskTextureEditor.Editor;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByMaskComponent))]
    internal class VertexFilterByMask : IVertexFilter
    {
        private readonly int _materialIndex;
        private readonly Texture2D _maskTexture;
        private readonly Hash128 _maskTextureContentHash;

        public int MaterialIndex => _materialIndex;
        public Texture2D MaskTexture => _maskTexture;
        public ByMaskMode DeleteMode { get; }

        private Texture2D _editingTexture;
        private Hash128 _editingTextureContentHash;

        public VertexFilterByMask(int materialIndex, Texture2D maskTexture, ByMaskMode deleteMode)
        {
            _materialIndex = materialIndex;
            _maskTexture = maskTexture;
            _maskTextureContentHash = maskTexture == null ? default : maskTexture.imageContentsHash;
            DeleteMode = deleteMode;
        }

        public VertexFilterByMask(VertexFilterByMaskComponent component, ComputeContext context) : this(
            component.MaterialIndex, component.MaskTexture, component.DeleteMode)
        {
            context.Observe(component, c => (c.MaterialIndex, c.MaskTexture, c.DeleteMode));
            if (component.MaskTexture != null)
            {
                context.Observe(component.MaskTexture, tex => tex.imageContentsHash);
            }
        }

        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0 || _maskTexture == null)
            {
                // TODO: add an appropriate error report here
                return;
            }

            Texture2D? tempTexture = null;
            try
            {
                var targetTexture = _editingTexture ?? _maskTexture;
                if (!targetTexture.isReadable)
                {
                    // We need a readable texture to read pixels from it, so copy the non-readable texture to a
                    // readable temporary texture. This requires taking a trip through a render texture first.
                    var tempRT = RenderTexture.GetTemporary(targetTexture.width, targetTexture.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    var oldActive = RenderTexture.active;
                    try
                    {
                        tempTexture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.ARGB32,
                            false);
                        Graphics.Blit(targetTexture, tempRT);
                        tempTexture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
                        tempTexture.Apply(false);
                        tempTexture.wrapModeU = targetTexture.wrapModeU;
                        tempTexture.wrapModeV = targetTexture.wrapModeV;
                        tempTexture.filterMode = targetTexture.filterMode;
                        targetTexture = tempTexture;
                    }
                    finally
                    {
                        RenderTexture.active = oldActive;
                        RenderTexture.ReleaseTemporary(tempRT);
                    }
                }

                var subMeshIndices = Enumerable.Range(0, mesh.subMeshCount)
                    .Select(x => mesh.GetIndices(x).ToHashSet())
                    .ToArray();

                foreach (var v in subMeshIndices[Mathf.Min(_materialIndex, subMeshIndices.Length - 1)])
                {
                    Color? deleteColor = DeleteMode switch
                    {
                        ByMaskMode.DeleteBlack => Color.black,
                        ByMaskMode.DeleteWhite => Color.white,
                        _ => null
                    };
                    if (targetTexture.GetPixel((int)(targetTexture.width * uv[v].x),
                            (int)(targetTexture.height * uv[v].y)) == deleteColor)
                    {
                        filtered[v] = true;
                    }
                }
            }
            finally
            {
                if (tempTexture != null) Object.DestroyImmediate(tempTexture);
            }
        }

#if MA_MASK_TEXTURE_EDITOR
        public void Observe(ComputeContext context)
        {
            _editingTexture = Window.ObserveTextureFor(context, _maskTexture, null, null,
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
                   && filter.DeleteMode == DeleteMode;
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
                DeleteMode);
        }

        public override string ToString()
        {
            return $"{_materialIndex}_{_maskTexture?.GetInstanceID()}_{DeleteMode}";
        }
    }
}