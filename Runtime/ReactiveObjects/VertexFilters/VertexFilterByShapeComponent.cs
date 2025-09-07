using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Vertex Filters/MA Vertex Filter - By Blendshape")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class VertexFilterByShapeComponent : AvatarTagComponent, IVertexFilterBehavior
    {
        [SerializeField] internal float m_threshold = 0.001f;
        [SerializeField] internal string[] m_shapes = Array.Empty<string>();

        public List<string> Shapes
        {
            get => m_shapes.ToList();
            set => m_shapes = value.ToArray();
        }

        public float Threshold
        {
            get => m_threshold;
            set => m_threshold = value;
        }
    }
}