#region

using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [HelpURL("https://m-a.nadena.dev/docs/intro?lang=auto")]
    internal class ModularAvatarInformation : ScriptableObject
    {
        internal static ModularAvatarInformation _instance;

        internal static ModularAvatarInformation instance
        {
            get
            {
                if (_instance == null) _instance = CreateInstance<ModularAvatarInformation>();
                return _instance;
            }
        }
    }
}