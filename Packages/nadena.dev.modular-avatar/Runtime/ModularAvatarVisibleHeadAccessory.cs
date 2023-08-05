using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Visible Head Accessory")]
    [DisallowMultipleComponent]
    public class ModularAvatarVisibleHeadAccessory : AvatarTagComponent
    {
        // no configuration needed
        internal override void ResolveReferences()
        {
            // no-op
        }
    }
}