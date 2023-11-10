#if MA_VRCSDK3_AVATARS

using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuInstallTarget))]
    internal class MenuInstallTargetEditor : MAEditorBase
    {
        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(ModularAvatarMenuInstallTarget.installer)));
            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif