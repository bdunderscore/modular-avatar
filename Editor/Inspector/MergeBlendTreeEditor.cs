﻿using UnityEditor;
using UnityEditor.Animations;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMergeBlendTree))]
    internal class MergeBlendTreeEditor : MAEditorBase
    {
        private SerializedProperty _blendTree;
        private SerializedProperty _pathMode;
        private SerializedProperty _relativePathRoot;
        
        private void OnEnable()
        {
            _blendTree = serializedObject.FindProperty(nameof(ModularAvatarMergeBlendTree.BlendTree));
            _pathMode = serializedObject.FindProperty(nameof(ModularAvatarMergeBlendTree.PathMode));
            _relativePathRoot = serializedObject.FindProperty(nameof(ModularAvatarMergeBlendTree.RelativePathRoot));
        }
        
        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.ObjectField(_blendTree, typeof(BlendTree), G("merge_blend_tree.blend_tree"));
            EditorGUILayout.PropertyField(_pathMode, G("merge_blend_tree.path_mode"));
            if (_pathMode.enumValueIndex == (int) MergeAnimatorPathMode.Relative)
            {
                EditorGUILayout.PropertyField(_relativePathRoot, G("merge_blend_tree.relative_path_root"));
            }
            
            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}