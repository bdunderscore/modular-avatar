using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuInstaller))]
    [CanEditMultipleObjects]
    public class MenuInstallerEditor : Editor
    {
        private ModularAvatarMenuInstaller _installer;
        private Editor _innerMenuEditor;
        private VRCExpressionsMenu _menuToAppend;

        private bool _menuFoldout;
        private bool _devFoldout;

        private HashSet<VRCExpressionsMenu> _avatarMenus;

        private void OnEnable()
        {
            _installer = (ModularAvatarMenuInstaller) target;

            FindMenus();
        }

        private void SetupMenuEditor()
        {
            if (targets.Length != 1)
            {
                _innerMenuEditor = null;
                _menuToAppend = null;
            }
            else if (_installer.menuToAppend != _menuToAppend)
            {
                if (_installer.menuToAppend == null) _innerMenuEditor = null;
                else
                {
                    _innerMenuEditor = CreateEditor(_installer.menuToAppend);
                }

                _menuToAppend = _installer.menuToAppend;
            }
        }

        public override void OnInspectorGUI()
        {
            SetupMenuEditor();

            var installTo = serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.installTargetMenu));

            if (!installTo.hasMultipleDifferentValues)
            {
                if (installTo.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "Select one of your avatar's menus below to automatically install controls for this prefab."
                        , MessageType.Info);
                }
                else if (!IsMenuReachable(RuntimeUtil.FindAvatarInParents(((Component) target).transform),
                             (VRCExpressionsMenu) installTo.objectReferenceValue))
                {
                    EditorGUILayout.HelpBox(
                        "Selected menu asset is not part of your avatar."
                        , MessageType.Error);
                }
            }

            EditorGUILayout.PropertyField(installTo, new GUIContent("Install To"));

            var avatar = RuntimeUtil.FindAvatarInParents(_installer.transform);
            if (avatar != null && GUILayout.Button("Select menu"))
            {
                AvMenuTreeViewWindow.Show(avatar, menu =>
                {
                    installTo.objectReferenceValue = menu;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            if (targets.Length == 1)
            {
                _menuFoldout = EditorGUILayout.Foldout(_menuFoldout, "Show menu contents");
                if (_menuFoldout)
                {
                    EditorGUI.indentLevel++;
                    using (var disabled = new EditorGUI.DisabledScope(true))
                    {
                        if (_innerMenuEditor != null) _innerMenuEditor.OnInspectorGUI();
                        else EditorGUILayout.HelpBox("No menu selected", MessageType.Info);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            _devFoldout = EditorGUILayout.Foldout(_devFoldout, "Developer Options");
            if (_devFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.menuToAppend)),
                    new GUIContent("Menu to install")
                );
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void FindMenus()
        {
            if (targets.Length > 1)
            {
                _avatarMenus = null;
                return;
            }

            _avatarMenus = new HashSet<VRCExpressionsMenu>();
            var queue = new Queue<VRCExpressionsMenu>();
            var avatar = RuntimeUtil.FindAvatarInParents(((Component) target).transform);
            if (avatar == null || avatar.expressionsMenu == null) return;
            queue.Enqueue(avatar.expressionsMenu);

            while (queue.Count > 0)
            {
                var menu = queue.Dequeue();
                if (_avatarMenus.Contains(menu)) continue;

                _avatarMenus.Add(menu);
                foreach (var subMenu in menu.controls)
                {
                    if (subMenu.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        queue.Enqueue(subMenu.subMenu);
                    }
                }
            }
        }

        private bool IsMenuReachable(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu)
        {
            return _avatarMenus == null || _avatarMenus.Contains(menu);
        }
    }
}