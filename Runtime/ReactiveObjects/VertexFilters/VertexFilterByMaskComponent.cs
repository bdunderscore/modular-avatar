using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [Serializable]
    [PublicAPI]
    public enum ByMaskMode
    {
        DeleteBlack,
        DeleteWhite
    }

    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Vertex Filters/MA Vertex Filter - By Mask")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class VertexFilterByMaskComponent : AvatarTagComponent, IVertexFilterBehavior
    {
        [SerializeField] internal int m_materialIndex;
        [SerializeField] internal Texture2D m_maskTexture;
        [SerializeField] internal ByMaskMode m_deleteMode;

        public int MaterialIndex
        {
            get => m_materialIndex;
            set => m_materialIndex = value;
        }

        public Texture2D MaskTexture
        {
            get => m_maskTexture;
            set => m_maskTexture = value;
        }

        public ByMaskMode DeleteMode
        {
            get => m_deleteMode;
            set => m_deleteMode = value;
        }
    }
}