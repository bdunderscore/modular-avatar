using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMMDLayerControl))]
    internal class MMDModeEditor : MAEditorBase
    {
        private SerializedProperty m_p_DisableInMMDMode;

        private void OnEnable()
        {
            m_p_DisableInMMDMode =
                serializedObject.FindProperty(nameof(ModularAvatarMMDLayerControl.m_DisableInMMDMode));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            LogoDisplay.DisplayLogo();

            EditorGUILayout.PropertyField(m_p_DisableInMMDMode, G("mmd_mode.disable_in_mmd_mode"));

            ShowLanguageUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}