using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarVRChatSettings))]
    internal class VRChatSettingsEditor : MAEditorBase
    {
        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(ModularAvatarVRChatSettings.m_mmdWorldSupport)),
                Localization.G("platform.vrchat.settings.mmd_world_support")
            );

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            Localization.ShowLanguageUI();
        }
    }
}