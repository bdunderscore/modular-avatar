using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class InspectorCommon
    {
        internal static void DisplayOutOfAvatarWarning(Object[] targets)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            if (targets.Length != 1) return;

            var target = targets[0] as Component;
            if (target == null) return;

            if (RuntimeUtil.FindAvatarInParents(target.transform) == null)
            {
                EditorGUILayout.HelpBox(Localization.S("hint.not_in_avatar"), MessageType.Warning);
            }
        }

        public static void DisplayVRCSDKVersionWarning()
        {
            EditorGUILayout.HelpBox(Localization.S("hint.bad_vrcsdk"), MessageType.Error);
        }
    }
}