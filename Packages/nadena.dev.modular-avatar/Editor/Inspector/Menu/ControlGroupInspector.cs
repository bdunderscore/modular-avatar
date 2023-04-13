using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ControlGroup))]
    internal class ControlGroupInspector : MAEditorBase
    {
        private bool _showInner;
        private SerializedProperty _isSynced, _isSaved;

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += Invalidate;

            _isSynced = serializedObject.FindProperty(nameof(ControlGroup.isSynced));
            _isSaved = serializedObject.FindProperty(nameof(ControlGroup.isSaved));
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
            foreach (var menuItem in menuItems.Where(item => item.controlGroup == target))
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

        protected override void OnInnerInspectorGUI()
        {
            if (_menuItemActions == null) Invalidate();

            EditorGUILayout.PropertyField(_isSynced, G("control_group.is_synced"));
            EditorGUILayout.PropertyField(_isSaved, G("control_group.is_saved"));

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

            Localization.ShowLanguageUI();
        }
    }
}