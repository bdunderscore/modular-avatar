using System;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMergeArmature))]
    public class MergeArmatureEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var target = (ModularAvatarMergeArmature) this.target;
            var priorMergeTarget = target.mergeTargetObject;

            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                if (target.mergeTargetObject != null && priorMergeTarget == null
                                                     && string.IsNullOrEmpty(target.prefix)
                                                     && string.IsNullOrEmpty(target.suffix))
                {
                    // We only infer if targeting the armature (below the Hips bone)
                    var rootAnimator = RuntimeUtil.FindAvatarInParents(target.transform)?.GetComponent<Animator>();
                    if (rootAnimator == null) return;

                    var hips = rootAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips == null || hips.transform.parent != target.mergeTargetObject.transform) return;

                    // We also require that the attached object has exactly one child (presumably the hips)
                    if (target.transform.childCount != 1) return;

                    // Infer the prefix and suffix by comparing the names of the mergeTargetObject's hips with the child of the
                    // GameObject we're attached to.
                    var baseName = hips.name;
                    var mergeName = target.transform.GetChild(0).name;

                    var prefixLength = mergeName.IndexOf(baseName, StringComparison.InvariantCulture);
                    if (prefixLength < 0) return;

                    var suffixLength = mergeName.Length - prefixLength - baseName.Length;

                    target.prefix = mergeName.Substring(0, prefixLength);
                    target.suffix = mergeName.Substring(mergeName.Length - suffixLength);

                    if (!string.IsNullOrEmpty(target.prefix) || !string.IsNullOrEmpty(target.suffix))
                    {
                        RuntimeUtil.MarkDirty(target);
                    }
                }
            }
        }
    }
}