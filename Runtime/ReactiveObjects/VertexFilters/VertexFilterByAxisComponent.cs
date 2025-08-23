using JetBrains.Annotations;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var refTransform = GetReferenceTransform();
            if (refTransform == null) return;

            var center = refTransform.TransformPoint(m_center);
            var axis = refTransform.TransformDirection(m_axis);
            var quat = Quaternion.LookRotation(axis, Vector3.up);

            Handles.DrawWireDisc(center, axis, 0.2f, 4.0f);
            Handles.ArrowHandleCap(0, center, quat, 0.2f, EventType.Repaint);
        }
#endif
    }
}