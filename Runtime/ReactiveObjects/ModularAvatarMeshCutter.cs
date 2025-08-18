using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [PublicAPI]
    [AddComponentMenu("Modular Avatar/MA Mesh Cutter")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-cutter?lang=auto")]
    public class ModularAvatarMeshCutter : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField] private AvatarObjectReference m_object = new();

        public AvatarObjectReference Object
        {
            get => m_object;
            set => m_object =
                value ?? throw new ArgumentNullException(nameof(value), "Object reference cannot be null");
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