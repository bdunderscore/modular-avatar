using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    public static class ApplyOnPlay
    {
        private const string MENU_NAME = "Tools/Modular Avatar/Apply on Play";

        static ApplyOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Menu.SetChecked(MENU_NAME, ModularAvatarSettings.applyOnPlay);
        }

        [MenuItem(MENU_NAME)]
        private static void ToggleApplyOnPlay()
        {
            ModularAvatarSettings.applyOnPlay = !ModularAvatarSettings.applyOnPlay;
            Menu.SetChecked(MENU_NAME, ModularAvatarSettings.applyOnPlay);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredPlayMode && ModularAvatarSettings.applyOnPlay)
            {
                // TODO - only apply modular avatar changes?
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>(true))
                    {
                        if (avatar.GetComponentsInChildren<AvatarTagComponent>(true).Length > 0)
                        {
                            VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar.gameObject);
                        }
                    }
                }
            }
        }
    }
}