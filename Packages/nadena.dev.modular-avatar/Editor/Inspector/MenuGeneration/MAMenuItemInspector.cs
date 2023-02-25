using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuItem))]
    [CanEditMultipleObjects]
    internal class MAMenuItemInspector : MAEditorBase
    {
        private SerializedProperty prop_control;
        private MenuItemCoreGUI _coreGUI;

        void OnEnable()
        {
            _coreGUI = new MenuItemCoreGUI(serializedObject, Repaint);
            _coreGUI.AlwaysExpandContents = true;

            serializedObject.FindProperty(nameof(ModularAvatarMenuItem.MenuSource));
            prop_control = serializedObject.FindProperty(nameof(ModularAvatarMenuItem.Control));
            prop_control.FindPropertyRelative(nameof(ModularAvatarMenuItem.Control.subMenu));
            serializedObject.FindProperty(nameof(ModularAvatarMenuItem.menuSource_otherObjectChildren));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            _coreGUI.DoGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}