using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ControlGroupDefaultDropdown : AdvancedDropdown
    {
        public string[] _names { get; private set; }
        public ModularAvatarMenuItem[] _menuItems { get; private set; }

        public event Action<ModularAvatarMenuItem> OnItemSelected;

        public ControlGroupDefaultDropdown(ModularAvatarMenuItem[] menuItems) : base(new AdvancedDropdownState())
        {
            _names = menuItems.Select(n =>
            {
                if (n == null || n.gameObject == null)
                {
                    return Localization.S("control_group.default_value.unset");
                }
                else
                {
                    return n.gameObject.name;
                }
            }).ToArray();
            _menuItems = menuItems;
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(S("control_group.default_value"));
            for (int i = 0; i < _names.Length; i++)
            {
                var item = new AdvancedDropdownItem(_names[i]);
                item.id = i;
                root.AddChild(item);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            OnItemSelected?.Invoke(_menuItems[item.id]);
        }
    }

    [CustomEditor(typeof(ControlGroup))]
    internal class ControlGroupInspector : MAEditorBase
    {
        private bool _showInner;
        private SerializedProperty _isSynced, _isSaved, _defaultValue;
        private ControlGroupDefaultDropdown _dropdown;

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += Invalidate;

            _isSynced = serializedObject.FindProperty(nameof(ControlGroup.isSynced));
            _isSaved = serializedObject.FindProperty(nameof(ControlGroup.isSaved));
            _defaultValue = serializedObject.FindProperty(nameof(ControlGroup.defaultValue));
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Invalidate;
        }

        private List<Action> _menuItemActions = null;

        private void Invalidate()
        {
            var target = (ControlGroup) this.target;
            var avatar = RuntimeUtil.FindAvatarInParents(target.transform);
            var menuItems = avatar.GetComponentsInChildren<ModularAvatarMenuItem>(true);

            _menuItemActions = new List<Action>();
            var filteredMenuItems = new List<ModularAvatarMenuItem>();
            foreach (var menuItem in menuItems.Where(item => item.controlGroup == target))
            {
                var node = CreateMenuItemNode(menuItem);
                _menuItemActions.Add(node);
                filteredMenuItems.Add(menuItem);
            }

            filteredMenuItems.Insert(0, null);
            _dropdown = new ControlGroupDefaultDropdown(filteredMenuItems.ToArray());
            _dropdown.OnItemSelected += (item) =>
            {
                serializedObject.Update();
                _defaultValue.objectReferenceValue = item;
                serializedObject.ApplyModifiedProperties();
            };
        }

        private Action CreateMenuItemNode(ModularAvatarMenuItem menuItem)
        {
            bool foldout = false;

            var coreUI = new MenuItemCoreGUI(new SerializedObject(menuItem), Repaint);
            var enableMenuItem = new SerializedObject(menuItem.gameObject).FindProperty("m_IsActive");

            List<Action> foldoutInspectors = null;

            return () =>
            {
                using (new MenuObjectHeader(menuItem, enableMenuItem).Scope())
                {
                    coreUI.DoGUI();

                    foldout = EditorGUILayout.Foldout(foldout, G("control_group.foldout.actions"));
                    if (foldout)
                    {
                        if (foldoutInspectors == null)
                        {
                            foldoutInspectors = menuItem.GetComponents<MenuAction>()
                                .Select(action =>
                                {
                                    var component = (Component) action;
                                    var editor = CreateEditor(component);
                                    var enabled_prop = new SerializedObject(component).FindProperty("m_Enabled");

                                    return (Action) (() =>
                                    {
                                        using (new MenuObjectHeader(component, enabled_prop).Scope())
                                        {
                                            editor.OnInspectorGUI();
                                        }
                                    });
                                })
                                .ToList();
                        }

                        foreach (var inspector in foldoutInspectors)
                        {
                            inspector();
                        }
                    }
                }
            };
        }

        private Rect _dropdownButtonRect;

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            if (_menuItemActions == null || _dropdown == null) Invalidate();

            EditorGUILayout.PropertyField(_isSynced, G("control_group.is_synced"));
            EditorGUILayout.PropertyField(_isSaved, G("control_group.is_saved"));
            //EditorGUILayout.PropertyField(_defaultValue, G("control_group.default_value")); // TODO - dropdown

            if (_dropdown != null)
            {
                var label = G("control_group.default_value");
                var position = EditorGUILayout.GetControlRect(true);
                position = EditorGUI.PrefixLabel(position, label);

                var currentValue = _defaultValue.objectReferenceValue;
                string selected;

                if (currentValue == null || !(currentValue is ModularAvatarMenuItem item) ||
                    !item.controlGroup == target)
                {
                    selected = S("control_group.default_value.unset");
                }
                else
                {
                    selected = item.gameObject.name;
                }

                if (GUI.Button(position, selected, EditorStyles.popup))
                {
                    _dropdown.Show(position);
                }
            }


            _showInner = EditorGUILayout.Foldout(_showInner, G("control_group.foldout.menu_items"));
            if (_showInner)
            {
                foreach (var action in _menuItemActions)
                {
                    try
                    {
                        EditorGUI.indentLevel++;
                        action();
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space(4);
                }
            }

            serializedObject.ApplyModifiedProperties();

            Localization.ShowLanguageUI();
        }
    }
}