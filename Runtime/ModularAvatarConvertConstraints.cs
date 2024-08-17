using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [DisallowMultipleComponent]
#if MA_VRCSDK3_AVATARS
    [AddComponentMenu("Modular Avatar/MA Convert Constraints")]
#else
    [AddComponentMenu("")]
#endif
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/convert-constraints?lang=auto")]
    public class ModularAvatarConvertConstraints : AvatarTagComponent
    {
    }
}