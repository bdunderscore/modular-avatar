using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarProbeAnchor))]
    [CanEditMultipleObjects]
    internal class ProbeAnchorEditor : MAEditorBase
    {
        private SerializedProperty prop_probeTarget;

        private void OnEnable()
        {
            prop_probeTarget = serializedObject.FindProperty(nameof(ModularAvatarProbeAnchor.probeTarget));
        }

        private void ShowParametersUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(prop_probeTarget, G("probeanchor.target"));

            serializedObject.ApplyModifiedProperties();
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox(S("probe_anchor.help"), MessageType.Info);
            EditorGUI.BeginChangeCheck();
            ShowParametersUI();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            Localization.ShowLanguageUI();
        }
    }
}