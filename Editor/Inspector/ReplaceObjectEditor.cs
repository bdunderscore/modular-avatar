using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarReplaceObject))]
    internal class ReplaceObjectEditor : MAEditorBase
    {
        private SerializedProperty _targetObject;

        protected void OnEnable()
        {
            _targetObject = serializedObject.FindProperty("targetObject");
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetObject, G("replace_object.target_object"));

            Localization.ShowLanguageUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}