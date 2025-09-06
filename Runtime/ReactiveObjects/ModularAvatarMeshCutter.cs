using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    ///     Selects how to determine the range of polygons to remove, when there are multiple Vertex Filters
    ///     in use.
    /// </summary>
    [Serializable]
    [PublicAPI]
    public enum MeshCutterMultiMode
    {
        VertexUnion,
        VertexIntersection
    }
    
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/MA Mesh Cutter")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class ModularAvatarMeshCutter : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField] private AvatarObjectReference m_object = new();
        [SerializeField] private MeshCutterMultiMode m_multiMode = MeshCutterMultiMode.VertexIntersection;

        public AvatarObjectReference Object
        {
            get => m_object;
            set => m_object =
                value ?? throw new ArgumentNullException(nameof(value), "Object reference cannot be null");
        }

        public MeshCutterMultiMode MultiMode
        {
            get => m_multiMode;
            set => m_multiMode = value;
        }

        public override void ResolveReferences()
        {
            m_object.Get(this);
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (m_object != null) yield return m_object;
        }
    }
}