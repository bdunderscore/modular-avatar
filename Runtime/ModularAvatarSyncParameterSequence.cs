using System;
using JetBrains.Annotations;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace nadena.dev.modular_avatar.core
{
    #if MA_VRCSDK3_AVATARS
    [AddComponentMenu("Modular Avatar/MA Sync Parameter Sequence")]
    #else
    [AddComponentMenu("")]
    #endif
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

        [Obsolete("After 1.16, parameter syncing is implicitly managed")]
        #if MA_VRCSDK3_AVATARS
        public VRCExpressionParameters Parameters;
        #else
        // preserve settings on non-VRC platforms at least
        public UnityEngine.Object Parameters;
        #endif

        private void Reset()
        {
            if (RuntimeUtil.IsResetFromInspector())
            {
                PrimaryPlatform = Platform.PC;
            }
        }
    }
}