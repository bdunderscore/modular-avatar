using System;
using JetBrains.Annotations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Sync Parameter Sequence")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/sync-parameter-sequence?lang=auto")]
    [PublicAPI]
    public class ModularAvatarSyncParameterSequence : AvatarTagComponent
    {
        [Serializable]
        [PublicAPI]
        public enum Platform
        {
            PC,
            Android,
            iOS
        }
        
        public Platform PrimaryPlatform = Platform.Android;
        #if MA_VRCSDK3_AVATARS
        public VRCExpressionParameters Parameters;
        #else
        // preserve settings on non-VRC platforms at least
        public UnityEngine.Object Parameters;
        #endif
    }
}