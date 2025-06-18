using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
	[AddComponentMenu("Modular Avatar/MA Platform Filter")]
	[HelpURL("https://modular-avatar.nadena.dev/docs/reference/platform-filter?lang=auto")]
    public class ModularAvatarPlatformFilter : AvatarTagComponent
    {
		[SerializeField]
        internal bool m_excludePlatform = true;
        [SerializeField]
		internal string m_platform;

		public bool ExcludePlatform {
			get => m_excludePlatform;
			set => m_excludePlatform = value;
		}

		public string Platform
		{
			get => m_platform;
			set => m_platform = value;
		}
    }
}