using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarObjectRemover))]
    internal class ObjectRemoverEditor : MAEditorBase
    {
        private SerializedProperty prop_hideInHierarchy;
        private SerializedProperty prop_keepDisabled;
        private SerializedProperty prop_removePrefabComponents;
        private SerializedProperty prop_objectsToRemove;
        ReorderableList myListReorderableList;

        private void OnEnable()
        {
            prop_hideInHierarchy = serializedObject.FindProperty(nameof(ModularAvatarObjectRemover.hideInHierarchy));
            prop_keepDisabled = serializedObject.FindProperty(nameof(ModularAvatarObjectRemover.keepDisabled));
            prop_removePrefabComponents = serializedObject.FindProperty(nameof(ModularAvatarObjectRemover.removePrefabComponents));
            prop_objectsToRemove = serializedObject.FindProperty(nameof(ModularAvatarObjectRemover.objectsToRemove));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();
            var remover = (ModularAvatarObjectRemover)target;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop_hideInHierarchy, G("object_remover.hide"));
            bool hideChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop_removePrefabComponents, G("object_remover.remove_components"));
            bool removeChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.PropertyField(prop_keepDisabled, G("object_remover.keep_disabled"));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop_objectsToRemove, G("object_remover.to_remove"));
            bool listChanged = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();
            if (hideChanged || removeChanged || listChanged) remover.OnListChange();
            if (hideChanged && !prop_hideInHierarchy.boolValue) remover.Unhide();
            if (removeChanged && !prop_removePrefabComponents.boolValue) remover.RestorePrefabComponents();
        }
    }
}