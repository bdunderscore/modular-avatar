using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Menu Installer")]
    public class ModularAvatarMenuInstaller : AvatarTagComponent
    { 
        public VRCExpressionsMenu menuToAppend; 
        public VRCExpressionsMenu installTargetMenu;


        // ReSharper disable once Unity.RedundantEventFunction
        void Start()
        { 
	        // Ensure that unity generates an enable checkbox
        } 
    }
}