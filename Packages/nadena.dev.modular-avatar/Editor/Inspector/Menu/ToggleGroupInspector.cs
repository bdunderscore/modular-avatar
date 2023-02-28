using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ToggleGroup))]
    internal class ToggleGroupInspector : MAEditorBase
    {
        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += Invalidate;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Invalidate;
        }

        private List<Action> _menuItemActions = null;

        private void Invalidate()
        {
            var target = (ToggleGroup) this.target;
            var avatar = RuntimeUtil.FindAvatarInParents(target.transform);
            var menuItems = avatar.GetComponentsInChildren<ModularAvatarMenuItem>(true);

            _menuItemActions = new List<Action>();
            foreach (var menuItem in menuItems.Where(item => item.toggleGroup == target))
            {
                var node = CreateMenuItemNode(menuItem);
                _menuItemActions.Add(node);
            }
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

                    foldout = EditorGUILayout.Foldout(foldout, "Actions");
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

        protected override void OnInnerInspectorGUI()
        {
            if (_menuItemActions == null) Invalidate();

            EditorGUILayout.LabelField("Bound menu items", EditorStyles.boldLabel);
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
    }
}