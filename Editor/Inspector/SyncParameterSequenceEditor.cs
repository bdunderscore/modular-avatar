#if MA_VRCSDK3_AVATARS

using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarSyncParameterSequence))]
    [CanEditMultipleObjects]
    public class SyncParameterSequenceEditor : MAEditorBase
    {
        private SerializedProperty _p_platform;

        private void OnEnable()
        {
            _p_platform = serializedObject.FindProperty(nameof(ModularAvatarSyncParameterSequence.PrimaryPlatform));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

#if MA_VRCSDK3_AVATARS
            var disable = false;
#else
            bool disable = true;
#endif

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (disable)
                // ReSharper disable HeuristicUnreachableCode
            {
                EditorGUILayout.HelpBox(S("general.vrcsdk-required"), MessageType.Warning);
            }
            // ReSharper restore HeuristicUnreachableCode

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            using (new EditorGUI.DisabledGroupScope(disable))
            {
                EditorGUILayout.PropertyField(_p_platform, G("sync-param-sequence.platform"));
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            ShowLanguageUI();
        }
    }
}

#endif