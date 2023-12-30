#if MA_VRM1

using nadena.dev.modular_avatar.core.vrm;
using UnityEditor;

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
            EditorGUILayout.PropertyField(_prop_collider_groups);
            EditorGUILayout.PropertyField(_prop_springs);
            serializedObject.ApplyModifiedProperties();
            Localization.ShowLanguageUI();
        }
    }
}

#endif
