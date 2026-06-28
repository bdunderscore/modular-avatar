#nullable enable

using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Vertex Filters/MA Vertex Filter - By UV Tile")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class VertexFilterByUVTileComponent : AvatarTagComponent, IMeshSelectorBehavior
    {
        [SerializeField] internal int m_uvChannel;
        [SerializeField] internal bool m_useUMin;
        [SerializeField] internal bool m_uMinInclusive;
        [SerializeField] internal float m_uMin;
        [SerializeField] internal bool m_useUMax;
        [SerializeField] internal bool m_uMaxInclusive;
        [SerializeField] internal float m_uMax = 1f;
        [SerializeField] internal bool m_useVMin;
        [SerializeField] internal bool m_vMinInclusive;
        [SerializeField] internal float m_vMin;
        [SerializeField] internal bool m_useVMax;
        [SerializeField] internal bool m_vMaxInclusive;
        [SerializeField] internal float m_vMax = 1f;
        [SerializeField] internal bool m_invert;
        [SerializeField] internal VertexSelectionMode m_selectionMode = VertexSelectionMode.AnyVertex;

        public int UVChannel
        {
            get => m_uvChannel;
            set => m_uvChannel = Mathf.Clamp(value, 0, 7);
        }

        public bool UseUMin
        {
            get => m_useUMin;
            set => m_useUMin = value;
        }

        public bool UMinInclusive
        {
            get => m_uMinInclusive;
            set => m_uMinInclusive = value;
        }

        public float UMin
        {
            get => m_uMin;
            set => m_uMin = value;
        }

        public bool UseUMax
        {
            get => m_useUMax;
            set => m_useUMax = value;
        }

        public bool UMaxInclusive
        {
            get => m_uMaxInclusive;
            set => m_uMaxInclusive = value;
        }

        public float UMax
        {
            get => m_uMax;
            set => m_uMax = value;
        }

        public bool UseVMin
        {
            get => m_useVMin;
            set => m_useVMin = value;
        }

        public bool VMinInclusive
        {
            get => m_vMinInclusive;
            set => m_vMinInclusive = value;
        }

        public float VMin
        {
            get => m_vMin;
            set => m_vMin = value;
        }

        public bool UseVMax
        {
            get => m_useVMax;
            set => m_useVMax = value;
        }

        public bool VMaxInclusive
        {
            get => m_vMaxInclusive;
            set => m_vMaxInclusive = value;
        }

        public float VMax
        {
            get => m_vMax;
            set => m_vMax = value;
        }

        public bool Invert
        {
            get => m_invert;
            set => m_invert = value;
        }

        public VertexSelectionMode SelectionMode
        {
            get => m_selectionMode;
            set => m_selectionMode = value;
        }
    }
}