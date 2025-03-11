#if MA_VRCSDK3_AVATARS

using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(MergeAnimatorPathMode))]
    class PathModeDrawer : EnumDrawer<MergeAnimatorPathMode>
    {
        protected override string localizationPrefix => "path_mode";
    }

    [CustomPropertyDrawer(typeof(MergeAnimatorMode))]
    internal class MergeModeDrawer : EnumDrawer<MergeAnimatorMode>
    {
        protected override string localizationPrefix => "merge_animator.merge_mode";
    }

    [CustomEditor(typeof(ModularAvatarMergeAnimator))]
    class MergeAnimationEditor : MAEditorBase
    {
        private SerializedProperty prop_animator,
            prop_layerType,
            prop_deleteAttachedAnimator,
            prop_pathMode,
            prop_matchAvatarWriteDefaults,
            prop_relativePathRoot,
            prop_layerPriority,
            prop_mergeMode;

        private void OnEnable()
        {
            prop_animator = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.animator));
            prop_layerType = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.layerType));
            prop_deleteAttachedAnimator =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.deleteAttachedAnimator));
            prop_pathMode = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.pathMode));
            prop_matchAvatarWriteDefaults =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.matchAvatarWriteDefaults));
            prop_relativePathRoot =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.relativePathRoot));
            prop_layerPriority = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.layerPriority));
            prop_mergeMode = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.mergeAnimatorMode));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(prop_animator, G("merge_animator.animator"));
            EditorGUILayout.PropertyField(prop_layerType, G("merge_animator.layer_type"));
            EditorGUILayout.PropertyField(prop_deleteAttachedAnimator, G("merge_animator.delete_attached_animator"));
            EditorGUILayout.PropertyField(prop_pathMode, G("merge_animator.path_mode"));
            if (prop_pathMode.enumValueIndex == (int) MergeAnimatorPathMode.Relative)
                EditorGUILayout.PropertyField(prop_relativePathRoot, G("merge_animator.relative_path_root"));
            EditorGUILayout.PropertyField(prop_layerPriority, G("merge_animator.layer_priority"));
            EditorGUILayout.PropertyField(prop_mergeMode, G("merge_animator.merge_mode"));
            using (new EditorGUI.DisabledScope(prop_mergeMode.enumValueIndex == (int)MergeAnimatorMode.Replace))
            {
                EditorGUILayout.PropertyField(prop_matchAvatarWriteDefaults,
                    G("merge_animator.match_avatar_write_defaults"));
            }

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}

#endif