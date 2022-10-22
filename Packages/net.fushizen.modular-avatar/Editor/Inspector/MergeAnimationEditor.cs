using UnityEditor;
using UnityEngine;
using static net.fushizen.modular_avatar.core.editor.Localization;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(MergeAnimatorPathMode))]
    class PathModeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            var value = EditorGUI.Popup(position, label, property.enumValueIndex, new GUIContent[]
            {
                G("path_mode.Relative"), G("path_mode.Absolute")
            });

            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = value;
            }

            EditorGUI.EndProperty();
        }
    }

    [CustomEditor(typeof(ModularAvatarMergeAnimator))]
    class MergeAnimationEditor : Editor
    {
        private SerializedProperty prop_animator,
            prop_layerType,
            prop_deleteAttachedAnimator,
            prop_pathMode,
            prop_matchAvatarWriteDefaults;

        private void OnEnable()
        {
            prop_animator = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.animator));
            prop_layerType = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.layerType));
            prop_deleteAttachedAnimator =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.deleteAttachedAnimator));
            prop_pathMode = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.pathMode));
            prop_matchAvatarWriteDefaults =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.matchAvatarWriteDefaults));
        }

        public override void OnInspectorGUI()
        {
            LogoDisplay.DisplayLogo();

            serializedObject.Update();

            EditorGUILayout.PropertyField(prop_animator, G("merge_animator.animator"));
            EditorGUILayout.PropertyField(prop_layerType, G("merge_animator.layer_type"));
            EditorGUILayout.PropertyField(prop_deleteAttachedAnimator, G("merge_animator.delete_attached_animator"));
            EditorGUILayout.PropertyField(prop_pathMode, G("merge_animator.path_mode"));
            EditorGUILayout.PropertyField(prop_matchAvatarWriteDefaults,
                G("merge_animator.match_avatar_write_defaults"));

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}