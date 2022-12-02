using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;
using static nadena.dev.modular_avatar.core.editor.Util;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuInstaller))]
    [CanEditMultipleObjects]
    internal class MenuInstallerEditor : MAEditorBase
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

        protected override void OnInnerInspectorGUI()
        {
            SetupMenuEditor();

            var installTo = serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.installTargetMenu));

            var isEnabled = targets.Length != 1 || ((ModularAvatarMenuInstaller) target).enabled;

            VRCAvatarDescriptor commonAvatar = FindCommonAvatar();

            if (!installTo.hasMultipleDifferentValues)
            {
                if (installTo.objectReferenceValue == null)
                {
                    if (isEnabled)
                    {
                        EditorGUILayout.HelpBox(S("menuinstall.help.hint_set_menu"), MessageType.Info);
                    }
                }
                else if (!IsMenuReachable(RuntimeUtil.FindAvatarInParents(((Component) target).transform),
                             (VRCExpressionsMenu) installTo.objectReferenceValue))
                {
                    EditorGUILayout.HelpBox(S("menuinstall.help.hint_bad_menu"), MessageType.Error);
                }
            }

            if (installTo.hasMultipleDifferentValues || commonAvatar == null)
            {
                EditorGUILayout.PropertyField(installTo, G("menuinstall.installto"));
            }
            else
            {
                var displayValue = installTo.objectReferenceValue;
                if (displayValue == null) displayValue = commonAvatar.expressionsMenu;

                EditorGUI.BeginChangeCheck();
                var newValue = EditorGUILayout.ObjectField(G("menuinstall.installto"), displayValue,
                    typeof(VRCExpressionsMenu), false);
                if (EditorGUI.EndChangeCheck())
                {
                    installTo.objectReferenceValue = newValue;
                }
            }

            var avatar = RuntimeUtil.FindAvatarInParents(_installer.transform);
            if (avatar != null && GUILayout.Button(G("menuinstall.selectmenu")))
            {
                AvMenuTreeViewWindow.Show(avatar, menu =>
                {
                    installTo.objectReferenceValue = menu;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            if (targets.Length == 1)
            {
                _menuFoldout = EditorGUILayout.Foldout(_menuFoldout, G("menuinstall.showcontents"));
                if (_menuFoldout)
                {
                    EditorGUI.indentLevel++;
                    using (var disabled = new EditorGUI.DisabledScope(true))
                    {
                        if (_innerMenuEditor != null) _innerMenuEditor.OnInspectorGUI();
                        else EditorGUILayout.HelpBox(S("menuinstall.showcontents.notselected"), MessageType.Info);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            _devFoldout = EditorGUILayout.Foldout(_devFoldout, G("menuinstall.devoptions"));
            if (_devFoldout) 
            {
                SerializedProperty menuToAppendProperty = serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.menuToAppend));
                switch (ValidateExpressionMenuIcon((VRCExpressionsMenu)menuToAppendProperty.objectReferenceValue)) 
                {
                    case ValidateExpressionMenuIconResult.Success:
                        break;
                    case ValidateExpressionMenuIconResult.TooLarge:
                        EditorGUILayout.HelpBox(S("menuinstall.menu_icon_too_large"), MessageType.Error);
                        break;
                    case ValidateExpressionMenuIconResult.Uncompressed:
                        EditorGUILayout.HelpBox(S("menuinstall.menu_icon_uncompressed"), MessageType.Error);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    menuToAppendProperty, new GUIContent(G("menuinstall.srcmenu")));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            Localization.ShowLanguageUI();
        }

        private VRCAvatarDescriptor FindCommonAvatar()
        {
            VRCAvatarDescriptor commonAvatar = null;

            foreach (var target in targets)
            {
                var component = (ModularAvatarMenuInstaller) target;
                var avatar = RuntimeUtil.FindAvatarInParents(component.transform);
                if (avatar == null) return null;

                if (commonAvatar == null)
                {
                    commonAvatar = avatar;
                }
                else if (commonAvatar != avatar)
                {
                    return null;
                }
            }

            return commonAvatar;
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

        private static ValidateExpressionMenuIconResult ValidateExpressionMenuIcon(VRCExpressionsMenu menu) {
            if (menu == null) return ValidateExpressionMenuIconResult.Success;
            
            foreach (VRCExpressionsMenu.Control control in menu.controls) 
            {
                // Control
                ValidateExpressionMenuIconResult result = Util.ValidateExpressionMenuIcon(control.icon);
                if (result != ValidateExpressionMenuIconResult.Success) return result;
                
                // Labels
                foreach (VRCExpressionsMenu.Control.Label label in control.labels) 
                {
                    ValidateExpressionMenuIconResult labelResult = Util.ValidateExpressionMenuIcon(label.icon);
                    if (labelResult != ValidateExpressionMenuIconResult.Success) return labelResult;
                }
                
                // SubMenu
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                ValidateExpressionMenuIconResult subMenuResult = ValidateExpressionMenuIcon(control.subMenu);
                if (subMenuResult != ValidateExpressionMenuIconResult.Success) return subMenuResult;
            }
            
            return ValidateExpressionMenuIconResult.Success;
        }
    }
}