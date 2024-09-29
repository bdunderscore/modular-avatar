using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Mesh Settings")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/mesh-settings?lang=auto")]
    public class ModularAvatarMeshSettings : AvatarTagComponent, IHaveObjReferences
    {
        internal static readonly Bounds DEFAULT_BOUNDS = new Bounds(Vector3.zero, Vector3.one * 2);

        [Serializable]
        public enum InheritMode
        {
            Inherit,
            Set,
            DontSet,
            // Inherit if there is parent settings, set otherwise
            SetOrInherit,
        }

        //[Header("Probe anchor configuration")]
        public InheritMode InheritProbeAnchor = InheritMode.Inherit;
        public AvatarObjectReference ProbeAnchor;

        //[Header("Bounds configuration")]
        public InheritMode InheritBounds = InheritMode.Inherit;
        public AvatarObjectReference RootBone;
        public Bounds Bounds = DEFAULT_BOUNDS;

        public override void ResolveReferences()
        {
            ProbeAnchor?.Get(this);
            RootBone?.Get(this);
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (ProbeAnchor != null) yield return ProbeAnchor;
            if (RootBone != null) yield return RootBone;
        }
    }
}