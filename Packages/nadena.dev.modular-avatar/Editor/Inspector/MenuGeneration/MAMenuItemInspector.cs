using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuItem))]
    [CanEditMultipleObjects]
    internal class MAMenuItemInspector : MAEditorBase
    {
        private SerializedProperty prop_submenu_source;
        private SerializedProperty prop_control;
        private SerializedProperty prop_extMenu;
        private SerializedProperty prop_otherObjChildren;

        private MenuItemCoreGUI _coreGUI;

        private Dictionary<ModularAvatarMenuItem, MenuItemCoreGUI> _innerItemGUI =
            new Dictionary<ModularAvatarMenuItem, MenuItemCoreGUI>();

        private List<MenuItemCoreGUI> _expMenuInnerItemGUI = new List<MenuItemCoreGUI>();
        private VRCExpressionsMenu _lastExpMenu;

        void OnEnable()
        {
            _coreGUI = new MenuItemCoreGUI(serializedObject, Repaint);
            _coreGUI.AlwaysExpandContents = true;

            prop_submenu_source = serializedObject.FindProperty(nameof(ModularAvatarMenuItem.MenuSource));
            prop_control = serializedObject.FindProperty(nameof(ModularAvatarMenuItem.Control));
            prop_extMenu = prop_control.FindPropertyRelative(nameof(ModularAvatarMenuItem.Control.subMenu));
            prop_otherObjChildren =
                serializedObject.FindProperty(nameof(ModularAvatarMenuItem.menuSource_otherObjectChildren));
        }

        private void DrawControlSettings(ModularAvatarMenuItem item)
        {
            if (!_innerItemGUI.TryGetValue(item, out var gui))
            {
                gui = new MenuItemCoreGUI(new SerializedObject(item), Repaint);
                _innerItemGUI.Add(item, gui);
            }

            gui.DoGUI();
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            _coreGUI.DoGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}