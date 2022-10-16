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
    public class BoneProxyEditor : Editor
    {
        private bool foldout = false;

        private Object[] objRefs;

        private void OnEnable()
        {
            objRefs = new Object[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                objRefs[i] = ScriptableObject.CreateInstance<TempObjRef>();
            }
        }

        public override void OnInspectorGUI()
        {
            GameObject parentAvatar = null;

            for (int i = 0; i < targets.Length; i++)
            {
                var t = (ModularAvatarBoneProxy) targets[i];
                var av = RuntimeUtil.FindAvatarInParents(t.transform);

                if (av != null && parentAvatar == null) parentAvatar = av.gameObject;
                if (av == null || parentAvatar != av.gameObject)
                {
                    base.OnInspectorGUI();
                    return;
                }

                ((TempObjRef) objRefs[i]).target = t.target;
            }

            var virtObj = new SerializedObject(objRefs);
            var virtProp = virtObj.FindProperty(nameof(TempObjRef.target));

            var currentTarget = targets.Length != 1 ? null : ((ModularAvatarBoneProxy) targets[0]).target;
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