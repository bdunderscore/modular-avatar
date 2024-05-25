#if MA_VRM0 || MA_VRM1

using nadena.dev.modular_avatar.core.vrm;
using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    [CustomEditor(typeof(ModularAvatarMergeVRMFirstPerson))]
    internal class MergeVRMFirstPersonEditor : MAEditorBase
    {
        private SerializedProperty _prop_renderers;

        private void OnEnable()
        {
            _prop_renderers = serializedObject.FindProperty(nameof(ModularAvatarMergeVRMFirstPerson.renderers));
        }
        
        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.PropertyField(_prop_renderers, G("merge_vrm_first_person.renderers"));
            serializedObject.ApplyModifiedProperties();
            ShowLanguageUI();
        }
    }
}

#endif