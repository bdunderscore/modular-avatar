using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMergeArmature))]
    internal class MergeArmatureEditor : MAEditorBase
    {
        private SerializedProperty prop_mergeTarget, prop_prefix, prop_suffix, prop_lock_mode, prop_mangleNames;

        private void OnEnable()
        {
            prop_mergeTarget = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.mergeTarget));
            prop_prefix = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.prefix));
            prop_suffix = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.suffix));
            prop_lock_mode = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.LockMode));
            prop_mangleNames = serializedObject.FindProperty(nameof(ModularAvatarMergeArmature.mangleNames));
        }

        private void ShowParametersUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(prop_mergeTarget, G("merge_armature.merge_target"));
            EditorGUILayout.PropertyField(prop_prefix, G("merge_armature.prefix"));
            EditorGUILayout.PropertyField(prop_suffix, G("merge_armature.suffix"));
            EditorGUILayout.PropertyField(prop_mangleNames, G("merge_armature.mangle_names"));

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Position lock mode", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical();

            FakeEnum(prop_lock_mode, ArmatureLockMode.NotLocked, "Not locked",
                "Merged armature does not sync its position with the base avatar.");
            FakeEnum(prop_lock_mode, ArmatureLockMode.BaseToMerge, "Base  =====> Target (Unidirectional)",
                "Moving the base avatar will move the merge armature. If you move the merged armature, it will not" +
                " affect the base avatar. This is useful when adding normal outfits, where you might want to adjust" +
                " the position of bones in the outfit.");
            FakeEnum(prop_lock_mode, ArmatureLockMode.BidirectionalExact, "Base <=====> Target (Bidirectional)",
                "The base armature and the merged armature will always have the same position. This is useful when " +
                "creating animations that are meant to target the base armature.\n\n" +
                "In order to activate this, your armatures must already be in the exact same position."
            );

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void FakeEnum(SerializedProperty propLockMode, ArmatureLockMode index, string label, string desc)
        {
            var val = !propLockMode.hasMultipleDifferentValues && propLockMode.enumValueIndex == (int) index;

            var selectionStyle = val ? (GUIStyle) "flow node 1" : (GUIStyle) "flow node 0";
            selectionStyle.padding = new RectOffset(0, 0, 0, 0);
            selectionStyle.margin = new RectOffset(0, 0, 5, 5);

            var boldLabel = EditorStyles.boldLabel;
            boldLabel.wordWrap = true;

            var normalLabel = EditorStyles.label;
            normalLabel.wordWrap = true;

            EditorGUILayout.BeginVertical(selectionStyle);

            EditorGUILayout.LabelField(label, boldLabel);
            var l1 = GUILayoutUtility.GetLastRect();
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(desc, normalLabel);
            var l2 = GUILayoutUtility.GetLastRect();

            EditorGUILayout.EndVertical();

            var rect = GUILayoutUtility.GetLastRect();

            if (GUI.Button(rect, GUIContent.none, selectionStyle))
            {
                propLockMode.enumValueIndex = (int) index;
            }

            EditorGUI.LabelField(l1, label, boldLabel);
            EditorGUI.LabelField(l2, desc, normalLabel);
        }

        protected override void OnInnerInspectorGUI()
        {
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

            EditorGUILayout.Separator();

            var enable_name_assignment =
                target.mergeTarget.Get(RuntimeUtil.FindAvatarInParents(target.transform)) != null;
            using (var scope = new EditorGUI.DisabledScope(!enable_name_assignment))
            {
                if (GUILayout.Button(G("merge_armature.adjust_names")))
                {
                    HeuristicBoneMapper.RenameBonesByHeuristic(target);
                }
            }

            Localization.ShowLanguageUI();
        }
    }
}