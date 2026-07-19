using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    ///     Identifies the root of an outfit configured by Setup Outfit and provides access to its armature root.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Outfit Root")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/outfit-root?lang=auto")]
    public class ModularAvatarOutfitRoot : AvatarTagComponent
    {
        public Transform armatureRoot;
    }
}