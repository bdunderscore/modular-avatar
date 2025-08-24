using System;
using JetBrains.Annotations;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Vertex Filters/MA Vertex Filter - By Blendshape")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class VertexFilterByShapeComponent : AvatarTagComponent, IVertexFilterBehavior
    {
        [SerializeField] internal string m_shapeName = "";
        [SerializeField] internal float m_threshold = 0.001f;

        public string ShapeName
        {
            get => m_shapeName;
            set => m_shapeName = value;
        }

        public float Threshold
        {
            get => m_threshold;
            set => m_threshold = value;
        }
    }
}