using UnityEditor;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    public static class ModularAvatarSettings
    {
        private const string PREFKEY_APPLY_ON_PLAY = "net.fushizen.modular-avatar.applyOnPlay";
#if UNITY_EDITOR
        public static bool applyOnPlay
        {
            get => EditorPrefs.GetBool(PREFKEY_APPLY_ON_PLAY, true);
            set => EditorPrefs.SetBool(PREFKEY_APPLY_ON_PLAY, value);
        }
        #else
        public static bool applyOnPlay = false;
        #endif
    }
}