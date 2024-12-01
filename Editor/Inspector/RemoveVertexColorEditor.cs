using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(ModularAvatarRemoveVertexColor.RemoveMode))]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class RVCModeDrawer : EnumDrawer<ModularAvatarRemoveVertexColor.RemoveMode>
    {
        protected override string localizationPrefix => "remove-vertex-color.mode";
    }

    [CustomEditor(typeof(ModularAvatarRemoveVertexColor))]
    internal class RemoveVertexColorEditor : MAEditorBase
    {
        private SerializedProperty _p_mode;

        protected void OnEnable()
        {
            _p_mode = serializedObject.FindProperty(nameof(ModularAvatarRemoveVertexColor.Mode));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_p_mode, G("remove-vertex-color.mode"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            ShowLanguageUI();
        }
    }
}