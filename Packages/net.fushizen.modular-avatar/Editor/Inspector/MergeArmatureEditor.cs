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
                    target.InferPrefixSuffix();
                }
            }
        }
    }
}