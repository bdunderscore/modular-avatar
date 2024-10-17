using System;
using System.Collections.Generic;
using System.Linq;
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
        private bool posReset_convertATPose = true;
        private bool posReset_adjustRotation = false;
        private bool posReset_adjustScale = false;
        private bool posReset_heuristicRootScale = true;

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

            var enable_name_assignment = target.mergeTarget.Get(target) != null;
            using (var scope = new EditorGUI.DisabledScope(!enable_name_assignment))
            {
                if (GUILayout.Button(G("merge_armature.adjust_names")))
                {
                    var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(target.mergeTarget.Get(target).transform);
                    var avatarAnimator = avatarRoot != null ? avatarRoot.GetComponent<Animator>() : null;

                    // Search Outfit Root Animator
                    var outfitRoot = ((ModularAvatarMergeArmature)serializedObject.targetObject).transform;
                    Animator outfitAnimator = null;
                    while (outfitRoot != null)
                    {
                        if (outfitRoot == avatarRoot)
                        {
                            outfitAnimator = null;
                            break;
                        }
                        outfitAnimator = outfitRoot.GetComponent<Animator>();
                        if (outfitAnimator != null && outfitAnimator.isHuman) break;
                        outfitAnimator = null;
                        outfitRoot = outfitRoot.parent;
                    }

                    var outfitHumanoidBones = SetupOutfit.GetOutfitHumanoidBones(outfitRoot, outfitAnimator);
                    HeuristicBoneMapper.RenameBonesByHeuristic(target, outfitHumanoidBones: outfitHumanoidBones, avatarAnimator: avatarAnimator);
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

                        posReset_heuristicRootScale = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.heuristic_scale"),
                            posReset_heuristicRootScale);
                        posReset_convertATPose = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.convert_atpose"),
                            posReset_convertATPose);
                        posReset_adjustRotation = EditorGUILayout.ToggleLeft(
                            G("merge_armature.reset_pos.adjust_rotation"),
                            posReset_adjustRotation);
                        posReset_adjustScale = EditorGUILayout.ToggleLeft(G("merge_armature.reset_pos.adjust_scale"),
                            posReset_adjustScale);

                        if (GUILayout.Button(G("merge_armature.reset_pos.execute")))
                        {
                            ForcePositionToBaseAvatar();
                        }
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
            }

            Localization.ShowLanguageUI();
        }

        private void ForcePositionToBaseAvatar()
        {
            var mama = (ModularAvatarMergeArmature)target;
            
            ForcePositionToBaseAvatar(mama);
        }

        private void ForcePositionToBaseAvatar(ModularAvatarMergeArmature mama, bool suppressRootScale = false) {
            var mergeTarget = mama.mergeTarget.Get(mama);
            var xform_to_bone = new Dictionary<Transform, HumanBodyBones>();
            var bone_to_xform = new Dictionary<HumanBodyBones, Transform>();
            var rootAnimator = RuntimeUtil.FindAvatarTransformInParents(mergeTarget.transform)
                .GetComponent<Animator>();

            if (rootAnimator.isHuman)
            {
                foreach (var bone in Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>())
                {
                    if (bone != HumanBodyBones.LastBone)
                    {
                        var xform = rootAnimator.GetBoneTransform(bone);
                        if (xform != null)
                        {
                            xform_to_bone[xform] = bone;
                            bone_to_xform[bone] = xform;
                        }
                    }
                }
            }

            if (posReset_convertATPose)
            {
                SetupOutfit.FixAPose(RuntimeUtil.FindAvatarTransformInParents(mergeTarget.transform).gameObject, mama.transform, false);
            }

            if (posReset_heuristicRootScale && !suppressRootScale)
            {
                AdjustRootScale();
            }

            try
            {
                Walk(mama.transform, mergeTarget.transform);
            }
            finally
            {
                mama.ResetArmatureLock();
            }

            void AdjustRootScale()
            {
                // Adjust the overall scale of the avatar based on wingspan (arm length)
                if (!bone_to_xform.TryGetValue(HumanBodyBones.LeftHand, out var target_hand)) return;

                // Find the merge hand as well
                var hand_path = RuntimeUtil.RelativePath(mergeTarget, target_hand.gameObject);
                hand_path = string.Join("/", hand_path.Split('/').Select(elem => mama.prefix + elem + mama.suffix));

                var merge_hand = mama.transform.Find(hand_path);
                if (merge_hand == null) return;

                var target_wingspan = Mathf.Abs(rootAnimator.transform.InverseTransformPoint(target_hand.position).x);
                var merge_wingspan = Mathf.Abs(rootAnimator.transform.InverseTransformPoint(merge_hand.position).x);

                var scale = target_wingspan / merge_wingspan;
                mama.transform.localScale *= scale;
            }

            void Walk(Transform t_merge, Transform t_target)
            {
                Undo.RecordObject(t_merge, "Merge Armature: Force outfit position");
                
                Debug.Log("Merge: " + t_merge.gameObject.name + " => " + t_target.gameObject.name);
                
                t_merge.position = t_target.position;
                if (posReset_adjustScale)
                {
                    if (!posReset_heuristicRootScale || t_merge != mama.transform)
                    {
                        t_merge.localScale = t_target.localScale;
                    }
                }

                if (posReset_adjustRotation)
                {
                    t_merge.localRotation = t_target.localRotation;
                }

                Queue<Transform> traversalQueue = new Queue<Transform>();
                traversalQueue.Enqueue(t_merge);

                while (traversalQueue.Count > 0)
                {
                    foreach (Transform t_child in traversalQueue.Dequeue())
                    {
                        var mama_child = t_child.GetComponent<ModularAvatarMergeArmature>();
                        if (mama_child != null)
                        {
                            traversalQueue.Enqueue(t_child);
                            continue;
                        }
                    
                        if (TryMatchChildBone(t_target, t_child, out var t_target_child))
                        {
                            Walk(t_child, t_target_child);
                        }
                    }
                }
            }

            bool TryMatchChildBone(Transform t_target, Transform t_child, out Transform t_target_child)
            {
                var childName = t_child.gameObject.name;

                t_target_child = null;
                if (childName.StartsWith(mama.prefix) && childName.EndsWith(mama.suffix))
                {
                    var targetObjectName = childName.Substring(mama.prefix.Length,
                        childName.Length - mama.prefix.Length - mama.suffix.Length);
                    t_target_child = t_target.transform.Find(targetObjectName);
                }

                return t_target_child != null;
            }
        }
    }
}
