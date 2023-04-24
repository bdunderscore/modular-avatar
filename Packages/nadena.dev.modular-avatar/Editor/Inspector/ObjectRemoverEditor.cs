using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarObjectRemover))]
    internal class ObjectRemoverEditor : MAEditorBase
    {
        private SerializedProperty prop_hideInHierarchy;

        private void OnEnable()
        {
            prop_hideInHierarchy = serializedObject.FindProperty(nameof(ModularAvatarObjectRemover.hideInHierarchy));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();
            if (GUILayout.Button(G("object_remover.hide")))
            {
                prop_hideInHierarchy.boolValue = true;
                ((ModularAvatarObjectRemover)target).Hide();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}