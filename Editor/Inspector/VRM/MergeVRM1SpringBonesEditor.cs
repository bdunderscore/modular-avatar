#if MA_VRM1

using nadena.dev.modular_avatar.core.vrm;
using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    [CustomEditor(typeof(ModularAvatarMergeVRM1SpringBones))]
    internal class MergeVRM1SpringBonesEditor : MAEditorBase
    {
        private SerializedProperty _prop_collider_groups;
        private SerializedProperty _prop_springs;

        private void OnEnable()
        {
            _prop_collider_groups = serializedObject.FindProperty(nameof(ModularAvatarMergeVRM1SpringBones.colliderGroups));
            _prop_springs = serializedObject.FindProperty(nameof(ModularAvatarMergeVRM1SpringBones.springs));
        }
        
        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.PropertyField(_prop_collider_groups, G("merge_vrm1_spring_bones.collider_groups"));
            EditorGUILayout.PropertyField(_prop_springs, G("merge_vrm1_spring_bones.springs"));
            serializedObject.ApplyModifiedProperties();
            ShowLanguageUI();
        }
    }
}

#endif
