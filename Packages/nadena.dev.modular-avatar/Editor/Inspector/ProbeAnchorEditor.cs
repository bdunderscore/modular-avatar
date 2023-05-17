using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarProbeAnchor))]
    [CanEditMultipleObjects]
    internal class ProbeAnchorEditor : MAEditorBase
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
            EditorGUILayout.HelpBox(S("probe_anchor.help"), MessageType.Info);
            GameObject parentAvatar = null;

            bool suppressTarget = false;
            for (int i = 0; i < targets.Length; i++)
            {
                var t = (ModularAvatarProbeAnchor) targets[i];
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
                    var validationResult = ProbeAnchorProcessor.ValidateTarget(parentAvatar, targetTransform);
                    if (validationResult != ProbeAnchorProcessor.ValidationResult.OK)
                    {
                        EditorGUILayout.HelpBox(S("probeanchor.err." + validationResult), MessageType.Error);
                    }
                }

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(virtProp, G("probeanchor.target"));
                if (EditorGUI.EndChangeCheck())
                {
                    virtObj.ApplyModifiedPropertiesWithoutUndo();
                    for (int i = 0; i < targets.Length; i++)
                    {
                        var t = (ModularAvatarProbeAnchor) targets[i];
                        Undo.RecordObjects(targets, "Set targets");
                        var xform = ((TempObjRef) objRefs[i]).target;
                        if (RuntimeUtil.FindAvatarInParents(xform)?.gameObject != parentAvatar) continue;
                        t.target = xform;
                    }
                }
            }

            foldout = EditorGUILayout.Foldout(foldout, G("probeanchor.foldout.advanced"));
            if (foldout)
            {
                EditorGUI.indentLevel++;

                var p_boneReference = serializedObject.FindProperty(nameof(ModularAvatarProbeAnchor.boneReference));
                var p_subPath = serializedObject.FindProperty(nameof(ModularAvatarProbeAnchor.subPath));

                EditorGUILayout.PropertyField(p_boneReference, new GUIContent("Bone reference"));
                EditorGUILayout.PropertyField(p_subPath, new GUIContent("Sub path"));

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}