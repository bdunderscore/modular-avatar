#nullable enable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/Vertex Filters/MA Vertex Filter - By Bone")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class VertexFilterByBoneComponent : AvatarTagComponent, IVertexFilterBehavior, IHaveObjReferences
    {
        [SerializeField] internal AvatarObjectReference m_bone = new AvatarObjectReference();
        [SerializeField, Range(0f, 1f)] internal float m_threshold = 0.01f;

        public AvatarObjectReference Bone
        {
            get => m_bone;
        }

        public float Threshold
        {
            get => m_threshold;
            set => m_threshold = Mathf.Clamp01(value);
        }

        public override void ResolveReferences()
        {
            m_bone?.Get(this);
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (m_bone != null) yield return m_bone;
        }

        internal Transform? GetReferenceTransform()
        {
            if (!TryGetComponent<ModularAvatarMeshCutter>(out var cutter)) return null;
            var obj = cutter.Object?.Get(cutter);
            if (obj == null) return null;
            if (!obj.TryGetComponent<Renderer>(out var renderer)) return null;

            return renderer.transform;
        }
    }
}