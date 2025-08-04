using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [PublicAPI]
    [Serializable]
    public enum ByAxisReferenceFrame
    {
        RootBone,
        Renderer,
        AvatarRoot
    }
    
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Delete Mesh/MA Vertex Filter - By Axis")]
    public class VertexFilterByAxisComponent : AvatarTagComponent, IVertexFilterBehavior
    {
        internal static Action onDrawGizmos = () => { };

        private void OnDrawGizmosSelected()
        {
            onDrawGizmos();
        }

        [SerializeField] internal Vector3 m_center = Vector3.zero;
        [SerializeField] internal Vector3 m_axis = Vector3.left;
        [SerializeField] internal ByAxisReferenceFrame m_referenceFrame = ByAxisReferenceFrame.RootBone;

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

        public ByAxisReferenceFrame ReferenceFrame
        {
            get => m_referenceFrame;
            set => m_referenceFrame = value;
        }
    }
}