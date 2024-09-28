#region

using System;
using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class TempObjRef : ScriptableObject
    {
        public Transform target;
    }

    [CustomPropertyDrawer(typeof(BoneProxyAttachmentMode))]
    internal class BoneProxyAttachmentModeDrawer : EnumDrawer<BoneProxyAttachmentMode>
    {
        protected override string localizationPrefix => "boneproxy.attachment";

        protected override Array enumValues => new object[]
        {
            BoneProxyAttachmentMode.AsChildAtRoot,
            BoneProxyAttachmentMode.AsChildKeepWorldPose,
            BoneProxyAttachmentMode.AsChildKeepRotation,
            BoneProxyAttachmentMode.AsChildKeepPosition,
        };
    }

    [CustomEditor(typeof(ModularAvatarBoneProxy))]
    [CanEditMultipleObjects]
    internal class BoneProxyEditor : MAEditorBase
    {
        private bool foldout = false;

        private Object[] objRefs;

        private void OnEnable()
        {
            objRefs = new Object[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                objRefs[i] = CreateInstance<TempObjRef>();
            }
        }

        protected override void OnInnerInspectorGUI()
        {
            GameObject parentAvatar = null;

            bool suppressTarget = false;
            for (int i = 0; i < targets.Length; i++)
            {
                var t = (ModularAvatarBoneProxy) targets[i];
                var avTr = RuntimeUtil.FindAvatarTransformInParents(t.transform);

                if (avTr != null && parentAvatar == null) parentAvatar = avTr.gameObject;
                if (avTr == null || parentAvatar != avTr.gameObject)
                {
                    suppressTarget = true;
                    break;
                }

                ((TempObjRef) objRefs[i]).target = t.target;
            }

            if (suppressTarget)
            {
                foldout = true;
            }
            else
            {
                var virtObj = new SerializedObject(objRefs);
                var virtProp = virtObj.FindProperty(nameof(TempObjRef.target));

                if (virtProp.objectReferenceValue is Transform targetTransform)
                {
                    var validationResult = BoneProxyProcessor.ValidateTarget(parentAvatar, targetTransform);
                    if (validationResult != BoneProxyProcessor.ValidationResult.OK)
                    {
                        EditorGUILayout.HelpBox(S("boneproxy.err." + validationResult), MessageType.Error);
                    }
                }

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(virtProp, G("boneproxy.target"));
                if (EditorGUI.EndChangeCheck())
                {
                    virtObj.ApplyModifiedPropertiesWithoutUndo();
                    for (int i = 0; i < targets.Length; i++)
                    {
                        var t = (ModularAvatarBoneProxy) targets[i];
                        Undo.RecordObjects(targets, "Set targets");
                        var xform = ((TempObjRef) objRefs[i]).target;
                        if (xform != null && RuntimeUtil.FindAvatarTransformInParents(xform)?.gameObject != parentAvatar) continue;
                        t.target = xform;
                    }
                }
            }

            for (int i = 0; i < targets.Length; i++)
            {
                CheckAttachmentMode(targets[i] as ModularAvatarBoneProxy);
            }

            serializedObject.UpdateIfRequiredOrScript();
            var p_attachmentMode = serializedObject.FindProperty(nameof(ModularAvatarBoneProxy.attachmentMode));
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(p_attachmentMode, G("boneproxy.attachment"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var target in targets)
                {
                    var t = (ModularAvatarBoneProxy)target;
                    Undo.RecordObject(t.transform, "");
                    t.Update();
                }
            }

            foldout = EditorGUILayout.Foldout(foldout, G("boneproxy.foldout.advanced"));
            if (foldout)
            {
                EditorGUI.indentLevel++;

                var p_boneReference = serializedObject.FindProperty(nameof(ModularAvatarBoneProxy.boneReference));
                var p_subPath = serializedObject.FindProperty(nameof(ModularAvatarBoneProxy.subPath));

                EditorGUILayout.PropertyField(p_boneReference, new GUIContent("Bone reference"));
                EditorGUILayout.PropertyField(p_subPath, new GUIContent("Sub path"));

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }

        private void CheckAttachmentMode(ModularAvatarBoneProxy boneProxy)
        {
            if (boneProxy.attachmentMode == BoneProxyAttachmentMode.Unset && boneProxy.target != null)
            {
                float posDelta = Vector3.Distance(boneProxy.transform.position, boneProxy.target.position);
                float rotDelta = Quaternion.Angle(boneProxy.transform.rotation, boneProxy.target.rotation);

                Undo.RecordObject(boneProxy, "Configuring bone proxy attachment mode");
                if (posDelta > 0.001f || rotDelta > 0.001f)
                {
                    boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
                }
                else
                {
                    boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
                }
            }
        }
    }
}
