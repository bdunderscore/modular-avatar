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
            EditorGUILayout.LabelField(S("merge_armature.lockmode"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical();

            FakeEnum(prop_lock_mode, ArmatureLockMode.NotLocked, S("merge_armature.lockmode.not_locked.title"),
                S("merge_armature.lockmode.not_locked.body"));
            FakeEnum(prop_lock_mode, ArmatureLockMode.BaseToMerge, S("merge_armature.lockmode.base_to_merge.title"),
                S("merge_armature.lockmode.base_to_merge.body"));
            FakeEnum(prop_lock_mode, ArmatureLockMode.BidirectionalExact,
                S("merge_armature.lockmode.bidirectional.title"),
                S("merge_armature.lockmode.bidirectional.body"));

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void FakeEnum(SerializedProperty propLockMode, ArmatureLockMode index, string label, string desc)
        {
            var val = !propLockMode.hasMultipleDifferentValues && propLockMode.enumValueIndex == (int) index;

            var selectionStyle = new GUIStyle(val ? (GUIStyle) "flow node 1" : (GUIStyle) "flow node 0");
            selectionStyle.padding = new RectOffset(0, 0, 0, 0);
            selectionStyle.margin = new RectOffset(0, 0, 5, 5);

            var boldLabel = new GUIStyle(EditorStyles.boldLabel);
            boldLabel.wordWrap = true;

            var normalLabel = new GUIStyle(EditorStyles.label);
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

        private bool posResetOptionFoldout = false;
        private readonly MergeArmaturePositionResetOptions _resetOptions = new();

        protected override void OnInnerInspectorGUI()
        {
            var target = (ModularAvatarMergeArmature) this.target;
            var priorMergeTarget = target.mergeTargetObject;

            EditorGUI.BeginChangeCheck();
            ShowParametersUI();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                if (target.mergeTargetObject != null && priorMergeTarget != target.mergeTargetObject
                                                     && string.IsNullOrEmpty(target.prefix)
                                                     && string.IsNullOrEmpty(target.suffix))
                {
                    target.InferPrefixSuffix();
                }
            }

            EditorGUILayout.Separator();

            var enable_name_assignment = MergeArmatureInspectorTools.HasValidTarget(target);
            using (var scope = new EditorGUI.DisabledScope(!enable_name_assignment))
            {
                if (GUILayout.Button(G("merge_armature.adjust_names")))
                {
                    MergeArmatureInspectorTools.AdjustNames(target);
                }

                if (GUILayout.Button(G("merge_armature.match_scale_adjusters")))
                {
                    MergeArmatureInspectorTools.MatchScaleAdjusters(target);
                }
            }

            EditorGUILayout.Separator();

            if (targets.Length == 1)
            {
                posResetOptionFoldout = EditorGUILayout.Foldout(posResetOptionFoldout, G("merge_armature.reset_pos"));
                if (posResetOptionFoldout)
                {
                    EditorGUI.indentLevel++;

                    try
                    {
                        EditorGUILayout.HelpBox(
                            S("merge_armature.reset_pos.info"),
                            MessageType.Info
                        );

                        _resetOptions.HeuristicRootScale = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.heuristic_scale"),
                            _resetOptions.HeuristicRootScale);
                        _resetOptions.ConvertATPose = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.convert_atpose"),
                            _resetOptions.ConvertATPose);
                        _resetOptions.AdjustRotation = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.adjust_rotation"),
                            _resetOptions.AdjustRotation);
                        _resetOptions.AdjustScale = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.adjust_scale"),
                            _resetOptions.AdjustScale);
                        if (GUILayout.Button(G("merge_armature.reset_pos.execute")))
                        {
                            MergeArmatureInspectorTools.ForcePositionToBaseAvatar(target, _resetOptions);
                        }
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
            }

            ShowLanguageUI();
        }

    }
}
