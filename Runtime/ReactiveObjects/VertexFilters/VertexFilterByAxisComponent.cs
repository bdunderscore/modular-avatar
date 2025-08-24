#nullable enable

using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Vertex Filters/MA Vertex Filter - By Axis")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class VertexFilterByAxisComponent : AvatarTagComponent, IVertexFilterBehavior
    {
        [SerializeField] internal Vector3 m_center = Vector3.zero;
        [SerializeField] internal Vector3 m_axis = Vector3.left;

        public Vector3 Center
        {
            get => m_center;
            set => m_center = value;
        }

        public Vector3 Axis
        {
            get => m_axis;
            set => m_axis = value;
        }

        internal Transform? GetReferenceTransform()
        {
            if (!TryGetComponent<ModularAvatarMeshCutter>(out var cutter)) return null;
            var obj = cutter.Object?.Get(cutter);
            if (obj == null) return null;
            if (!obj.TryGetComponent<Renderer>(out var renderer)) return null;

            return renderer.transform;
        }

        internal static Action<VertexFilterByAxisComponent> _OnDrawGizmosSelected = _ => { };

        private void OnDrawGizmosSelected()
        {
            _OnDrawGizmosSelected(this);
        }
    }
}