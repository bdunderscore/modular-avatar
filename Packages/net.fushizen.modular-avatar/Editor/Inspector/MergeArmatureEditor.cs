using System;
using UnityEditor;
using UnityEngine;
using static net.fushizen.modular_avatar.core.editor.Localization;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMergeArmature))]
    public class MergeArmatureEditor : Editor
    {
        private SerializedProperty prop_mergeTarget, prop_prefix, prop_suffix, prop_locked;

        private void OnEnable()
        {
            prop_mergeTarget = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.mergeTarget));
            prop_prefix = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.prefix));
            prop_suffix = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.suffix));
            prop_locked = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.locked));
        }

        private void ShowParametersUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(prop_mergeTarget, G("merge_armature.merge_target"));
            EditorGUILayout.PropertyField(prop_prefix, G("merge_armature.prefix"));
            EditorGUILayout.PropertyField(prop_suffix, G("merge_armature.suffix"));
            EditorGUILayout.PropertyField(prop_locked, G("merge_armature.locked"));

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            LogoDisplay.DisplayLogo();

            var target = (ModularAvatarMergeArmature) this.target;
            var priorMergeTarget = target.mergeTargetObject;

            EditorGUI.BeginChangeCheck();
            ShowParametersUI();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                if (target.mergeTargetObject != null && priorMergeTarget == null
                                                     && string.IsNullOrEmpty(target.prefix)
                                                     && string.IsNullOrEmpty(target.suffix))
                {
                    target.InferPrefixSuffix();
                }
            }

            Localization.ShowLanguageUI();
        }
    }
}