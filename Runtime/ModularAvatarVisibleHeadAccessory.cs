using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Visible Head Accessory")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/visible-head-accessory?lang=auto")]
    public class ModularAvatarVisibleHeadAccessory : AvatarTagComponent
    {
        // no configuration needed
        public override void ResolveReferences()
        {
            // no-op
        }
    }
}