using System;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum Platform
    {
        PC,
        Android,
        iOS
    }
    
    [AddComponentMenu("Modular Avatar/MA Sync Parameter Sequence")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/sync-parameter-sequence?lang=auto")]
    public class ModularAvatarSyncParameterSequence : AvatarTagComponent
    {
        public Platform PrimaryPlatform;
        public VRCExpressionParameters Parameters;
    }
}