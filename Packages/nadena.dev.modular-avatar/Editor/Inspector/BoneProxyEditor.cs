using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class TempObjRef : ScriptableObject
    {
        public Transform target;
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
                var av = RuntimeUtil.FindAvatarInParents(t.transform);

                if (av != null && parentAvatar == null) parentAvatar = av.gameObject;
                if (av == null || parentAvatar != av.gameObject)
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
                        if (RuntimeUtil.FindAvatarInParents(xform)?.gameObject != parentAvatar) continue;
                        t.target = xform;
                    }
                }
            }

            foldout = EditorGUILayout.Foldout(foldout, G("boneproxy.foldout.advanced"));
            if (foldout)
            {
                EditorGUI.indentLevel++;
                DrawDefaultInspector();
                EditorGUI.indentLevel--;
            }

            Localization.ShowLanguageUI();
        }
    }
}