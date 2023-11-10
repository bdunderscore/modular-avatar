#if MA_VRCSDK3_AVATARS

using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuItem))]
    [CanEditMultipleObjects]
    internal class MAMenuItemInspector : MAEditorBase
    {
        private MenuItemCoreGUI _coreGUI;

        void OnEnable()
        {
            _coreGUI = new MenuItemCoreGUI(serializedObject, Repaint);
            _coreGUI.AlwaysExpandContents = true;
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            _coreGUI.DoGUI();

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }

    [CustomEditor(typeof(ModularAvatarMenuGroup))]
    internal class MAMenuGroupInspector : MAEditorBase
    {
        private MenuPreviewGUI _previewGUI;
        private SerializedProperty _prop_target;

        void OnEnable()
        {
            _previewGUI = new MenuPreviewGUI(Repaint);
            _prop_target = serializedObject.FindProperty(nameof(ModularAvatarMenuGroup.targetObject));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_prop_target, G("menuitem.prop.source_override"));

            _previewGUI.DoGUI((ModularAvatarMenuGroup) target);

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}

#endif