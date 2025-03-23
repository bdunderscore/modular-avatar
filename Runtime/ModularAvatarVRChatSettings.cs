using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA VRChat Settings")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/vrchat-settings?lang=auto")]
    public class ModularAvatarVRChatSettings : AvatarTagComponent
    {
        [SerializeField] internal bool m_mmdWorldSupport = true;

        [PublicAPI]
        public bool MMDWorldSupport
        {
            get => m_mmdWorldSupport;
            set => m_mmdWorldSupport = value;
        }
    }
}