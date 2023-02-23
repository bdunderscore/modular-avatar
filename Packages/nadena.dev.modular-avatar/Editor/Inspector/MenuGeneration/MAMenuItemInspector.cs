using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuItem))]
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
            bool multiEdit = targets.Length != 1;

            serializedObject.Update();

            _coreGUI.DoGUI();
            serializedObject.ApplyModifiedProperties();

            if (multiEdit) return;

            var menuItem = (ModularAvatarMenuItem) target;
            if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                GUILayout.Space(EditorStyles.label.lineHeight);
                EditorGUILayout.LabelField("Sub Menu", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(prop_submenu_source);

                if (prop_submenu_source.enumValueIndex == (int) SubmenuSource.Children)
                {
                }


                switch (menuItem.MenuSource)
                {
                    case SubmenuSource.MenuAsset:
                    {
                        EditorGUILayout.PropertyField(prop_extMenu);
                        serializedObject.ApplyModifiedProperties();

                        var expMenu = prop_extMenu.objectReferenceValue as VRCExpressionsMenu;

                        if (expMenu == null || expMenu.controls == null)
                        {
                            _expMenuInnerItemGUI.Clear();
                            break;
                        }

                        if (_lastExpMenu != expMenu || _expMenuInnerItemGUI.Count != expMenu.controls.Count)
                        {
                            _expMenuInnerItemGUI.Clear();

                            var expMenuSO = new SerializedObject(expMenu);
                            var controls = expMenuSO.FindProperty(nameof(expMenu.controls));

                            for (int i = 0; i < controls.arraySize; i++)
                            {
                                var control = controls.GetArrayElementAtIndex(i);
                                var gui = new MenuItemCoreGUI(((ModularAvatarMenuItem) target).gameObject, control,
                                    Repaint);
                                _expMenuInnerItemGUI.Add(gui);
                            }

                            _lastExpMenu = expMenu;
                        }

                        foreach (var gui in _expMenuInnerItemGUI)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                            gui.DoGUI();

                            EditorGUILayout.EndVertical();
                        }

                        break;
                    }

                    case SubmenuSource.Children:
                    {
                        _lastExpMenu = null;

                        EditorGUILayout.PropertyField(prop_otherObjChildren);
                        serializedObject.ApplyModifiedProperties();

                        var source = menuItem.menuSource_otherObjectChildren != null
                            ? menuItem.menuSource_otherObjectChildren
                            : menuItem.gameObject;
                        foreach (Transform t in source.transform)
                        {
                            var child = t.GetComponent<ModularAvatarMenuItem>();
                            if (child == null) continue;

                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                            GUILayout.BeginHorizontal();
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.ObjectField(new GUIContent(), child, typeof(ModularAvatarMenuItem),
                                    true,
                                    GUILayout.ExpandWidth(true));
                            }

                            GUILayout.Space(20);
                            GUILayout.Label("Enabled", GUILayout.Width(50));
                            var childObject = t.gameObject;
                            EditorGUI.BeginChangeCheck();
                            var active = GUILayout.Toggle(childObject.activeSelf, new GUIContent(),
                                GUILayout.Width(EditorGUIUtility.singleLineHeight));
                            if (EditorGUI.EndChangeCheck())
                            {
                                childObject.SetActive(active);
                            }

                            GUILayout.EndHorizontal();

                            DrawControlSettings(child);

                            EditorGUILayout.EndVertical();
                        }

                        break;
                    }
                    default: break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}