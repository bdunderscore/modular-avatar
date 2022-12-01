using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.ModularAvatarMenuFolderCreator;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Menu Installer")]
    public class ModularAvatarMenuInstaller : AvatarTagComponent {
        public InstallTargetType InstallTargetType;
        public VRCExpressionsMenu menuToAppend;
        public VRCExpressionsMenu installTargetMenu;
        public ModularAvatarMenuFolderCreator installTargetFolderCreator;


        // ReSharper disable once Unity.RedundantEventFunction
        void Start()
        {
            // Ensure that unity generates an enable checkbox
        }
    }
}