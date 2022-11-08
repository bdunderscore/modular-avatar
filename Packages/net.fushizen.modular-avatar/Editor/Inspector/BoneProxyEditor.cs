using UnityEditor;
using UnityEngine;
using static net.fushizen.modular_avatar.core.editor.Localization;

namespace net.fushizen.modular_avatar.core.editor
{
    internal class TempObjRef : ScriptableObject
    {
        public Transform target;
    }

    [CustomEditor(typeof(ModularAvatarBoneProxy))]
    [CanEditMultipleObjects]
    internal class BoneProxyEditor : Editor
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

        public override void OnInspectorGUI()
        {
            LogoDisplay.DisplayLogo();
            InspectorCommon.DisplayOutOfAvatarWarning(targets);

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

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(virtProp, G("boneproxy.target"));
                if (EditorGUI.EndChangeCheck())
                {
                    virtObj.ApplyModifiedPropertiesWithoutUndo();
                    for (int i = 0; i < targets.Length; i++)
                    {
                        var t = (ModularAvatarBoneProxy) targets[i];
                        Undo.RecordObjects(targets, "Set targets");
                        t.target = ((TempObjRef) objRefs[i]).target;
                    }
                }
            }

            foldout = EditorGUILayout.Foldout(foldout, G("boneproxy.foldout.advanced"));
            if (foldout)
            {
                EditorGUI.indentLevel++;
                base.OnInspectorGUI();
                EditorGUI.indentLevel--;
            }

            Localization.ShowLanguageUI();
        }
    }
}