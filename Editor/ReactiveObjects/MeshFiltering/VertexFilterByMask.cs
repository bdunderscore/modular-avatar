#nullable enable

using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if MA_MASK_TEXTURE_EDITOR
using net.nekobako.MaskTextureEditor.Editor;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByMaskComponent))]
    internal class VertexFilterByMask : IMeshSelector
    {
        private readonly int _materialIndex;
        private readonly Texture2D _maskTexture;
        private readonly Hash128 _maskTextureContentHash;
        private readonly VertexSelectionMode _selectionMode;

        public int MaterialIndex => _materialIndex;
        public Texture2D MaskTexture => _maskTexture;
        public ByMaskMode DeleteMode { get; }

        private Texture2D? _editingTexture;
        private Hash128 _editingTextureContentHash;

        public VertexFilterByMask(
            int materialIndex,
            Texture2D maskTexture,
            ByMaskMode deleteMode,
            VertexSelectionMode selectionMode = VertexSelectionMode.AnyVertex
        )
        {
            _materialIndex = materialIndex;
            _maskTexture = maskTexture;
            _maskTextureContentHash = maskTexture == null ? default : maskTexture.imageContentsHash;
            DeleteMode = deleteMode;
            _selectionMode = selectionMode;
        }

        public VertexFilterByMask(VertexFilterByMaskComponent component, ComputeContext context) : this(
            component.MaterialIndex, component.MaskTexture, component.DeleteMode, component.SelectionMode)
        {
            context.Observe(component, c => (c.MaterialIndex, c.MaskTexture, c.DeleteMode, c.SelectionMode));
            if (component.MaskTexture != null)
            {
                context.Observe(component.MaskTexture, tex => tex.imageContentsHash);
            }
        }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            if (!IsSelectedMaterialSubmesh(job, submesh)) return default;

            if (_selectionMode == VertexSelectionMode.Centroid)
            {
                if (!job.MeshData.HasVertexAttribute(VertexAttribute.TexCoord0)) return default;

                var maskFilter = BuildMaskUVFilter();
                if (maskFilter == null) return default;

                return job.MarkPrimitivesFromUVFilter(this, maskFilter.Value, _selectionMode, submesh,
                    selectedPrimitives);
            }

            return job.MarkPrimitivesFromVertexIndices(
                this,
                () => BuildVertexMask(job),
                _selectionMode,
                submesh,
                selectedPrimitives
            );
        }

        private bool IsSelectedMaterialSubmesh(MeshSelectorJob job, int submesh)
        {
            var submeshCount = job.MeshData.subMeshCount;
            if (submeshCount == 0) return false;

            return submesh == Mathf.Clamp(_materialIndex, 0, submeshCount - 1);
        }

        private (NativeArray<bool>, JobHandle) BuildVertexMask(MeshSelectorJob job)
        {
            var mesh = job.OriginalMesh;
            var vertexMask = new NativeArray<bool>(mesh.vertexCount, Allocator.TempJob);

            if (_maskTexture == null || !job.MeshData.HasVertexAttribute(VertexAttribute.TexCoord0))
                return (vertexMask, default);

            byte? deleteRGB = DeleteMode switch
            {
                ByMaskMode.DeleteBlack => 0,
                ByMaskMode.DeleteWhite => 255,
                _ => (byte?)null
            };
            if (deleteRGB == null) return (vertexMask, default);

            var targetTexture = _editingTexture ?? _maskTexture;
            Texture2D? tempTexture = null;
            try
            {
                if (!targetTexture.isReadable)
                {
                    var tempRT = RenderTexture.GetTemporary(targetTexture.width, targetTexture.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    var oldActive = RenderTexture.active;
                    try
                    {
                        tempTexture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.ARGB32,
                            false);
                        BlitWithoutFiltering(targetTexture, tempRT);
                        RenderTexture.active = tempRT;
                        tempTexture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
                        tempTexture.Apply(false);
                        tempTexture.wrapModeU = targetTexture.wrapModeU;
                        tempTexture.wrapModeV = targetTexture.wrapModeV;
                        targetTexture = tempTexture;
                    }
                    finally
                    {
                        RenderTexture.active = oldActive;
                        RenderTexture.ReleaseTemporary(tempRT);
                    }
                }

                // Copy pixel data into a NativeArray so it can be read from a Burst job.
                // GetPixels32 handles format conversion (e.g. ARGB32 → Color32 field order).
                var pixelData = new NativeArray<Color32>(targetTexture.GetPixels32(), Allocator.TempJob);

                JobHandle uvDep = default;
                var uvData = job.GetUV(ref uvDep, 0);

                var jobHandle = new MarkVertexByMaskJob
                {
                    UV = uvData,
                    Pixels = pixelData,
                    Width = targetTexture.width,
                    Height = targetTexture.height,
                    WrapU = targetTexture.wrapModeU,
                    WrapV = targetTexture.wrapModeV,
                    DeleteRGB = deleteRGB.Value,
                    VertexMask = vertexMask
                }.Schedule(uvData.Length, 64, uvDep);

                return (vertexMask, jobHandle);
            }
            finally
            {
                // Pixel data has been copied into the NativeArray; the temp texture is no longer needed.
                if (tempTexture != null) Object.DestroyImmediate(tempTexture);
            }
        }

        private MaskUVFilter? BuildMaskUVFilter()
        {
            if (_maskTexture == null) return null;

            byte? deleteRGB = DeleteMode switch
            {
                ByMaskMode.DeleteBlack => 0,
                ByMaskMode.DeleteWhite => 255,
                _ => null
            };
            if (deleteRGB == null) return null;

            var targetTexture = _editingTexture ?? _maskTexture;
            Texture2D? tempTexture = null;
            try
            {
                if (!targetTexture.isReadable)
                {
                    var tempRT = RenderTexture.GetTemporary(targetTexture.width, targetTexture.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    var oldActive = RenderTexture.active;
                    try
                    {
                        tempTexture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.ARGB32,
                            false);
                        BlitWithoutFiltering(targetTexture, tempRT);
                        RenderTexture.active = tempRT;
                        tempTexture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
                        tempTexture.Apply(false);
                        tempTexture.wrapModeU = targetTexture.wrapModeU;
                        tempTexture.wrapModeV = targetTexture.wrapModeV;
                        targetTexture = tempTexture;
                    }
                    finally
                    {
                        RenderTexture.active = oldActive;
                        RenderTexture.ReleaseTemporary(tempRT);
                    }
                }

                return new MaskUVFilter
                {
                    Pixels = new NativeArray<Color32>(targetTexture.GetPixels32(), Allocator.TempJob),
                    Width = targetTexture.width,
                    Height = targetTexture.height,
                    WrapU = targetTexture.wrapModeU,
                    WrapV = targetTexture.wrapModeV,
                    DeleteRGB = deleteRGB.Value
                };
            }
            finally
            {
                if (tempTexture != null) Object.DestroyImmediate(tempTexture);
            }
        }

        private static void BlitWithoutFiltering(Texture2D sourceTexture, RenderTexture destination)
        {
            var oldFilterMode = sourceTexture.filterMode;
            try
            {
                // On OpenGL platforms (eg, our CI environment) we get texture blurring due to the default filtering
                // settings. Since we want a pixel-perfect copy, avoid this by setting point filtering temporarily
                // during the blit.
                sourceTexture.filterMode = FilterMode.Point;
                Graphics.Blit(sourceTexture, destination);
            }
            finally
            {
                sourceTexture.filterMode = oldFilterMode;
            }
        }

        [BurstCompile]
        private struct MarkVertexByMaskJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float2> UV;
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<Color32> Pixels;
            public int Width;
            public int Height;
            public TextureWrapMode WrapU;
            public TextureWrapMode WrapV;
            public byte DeleteRGB;
            public NativeArray<bool> VertexMask;

            public void Execute(int index)
            {
                float2 uv = UV[index];
                int px = ApplyWrap(uv.x, Width, WrapU);
                int py = ApplyWrap(uv.y, Height, WrapV);
                Color32 pixel = Pixels[py * Width + px];
                if (pixel.r == DeleteRGB && pixel.g == DeleteRGB && pixel.b == DeleteRGB)
                    VertexMask[index] = true;
            }

            public static int ApplyWrap(float coord, int size, TextureWrapMode mode)
            {
                int i = (int)math.floor(coord * size);
                switch (mode)
                {
                    case TextureWrapMode.Repeat:
                        i = i % size;
                        if (i < 0) i += size;
                        return i;
                    case TextureWrapMode.Mirror:
                        i = ((i % (2 * size)) + 2 * size) % (2 * size);
                        return i >= size ? 2 * size - 1 - i : i;
                    case TextureWrapMode.MirrorOnce:
                        if (i < 0) i = -1 - i;
                        return math.clamp(i, 0, size - 1);
                    default: // Clamp
                        return math.clamp(i, 0, size - 1);
                }
            }
        }

        [BurstCompile]
        private struct MaskUVFilter : MeshSelectorJob.IUVFilter
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<Color32> Pixels;
            public int Width;
            public int Height;
            public TextureWrapMode WrapU;
            public TextureWrapMode WrapV;
            public byte DeleteRGB;

            public bool IsVertexSelected(float2 uv)
            {
                var px = MarkVertexByMaskJob.ApplyWrap(uv.x, Width, WrapU);
                var py = MarkVertexByMaskJob.ApplyWrap(uv.y, Height, WrapV);
                var pixel = Pixels[py * Width + px];
                return pixel.r == DeleteRGB && pixel.g == DeleteRGB && pixel.b == DeleteRGB;
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

        public bool Equals(IMeshSelector other)
        {
            return other is VertexFilterByMask filter
                   && filter._materialIndex == _materialIndex
                   && filter._maskTexture == _maskTexture
                   && filter._maskTextureContentHash == _maskTextureContentHash
                   && filter._editingTexture == _editingTexture
                   && filter._editingTextureContentHash == _editingTextureContentHash
                   && filter.DeleteMode == DeleteMode
                   && filter._selectionMode == _selectionMode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IMeshSelector);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                _materialIndex,
                _maskTexture, _maskTextureContentHash,
                _editingTexture, _editingTextureContentHash,
                DeleteMode, _selectionMode);
        }

        public override string ToString()
        {
            return $"{_materialIndex}_{_maskTexture?.GetEntityId()}_{DeleteMode}_{_selectionMode}";
        }
    }
}
