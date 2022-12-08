using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;
using static nadena.dev.modular_avatar.core.editor.Util;
using static nadena.dev.modular_avatar.core.ModularAvatarSubMenuCreator;
using Object = UnityEngine.Object;

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
        private HashSet<ModularAvatarSubMenuCreator> _menuFolderCreators;

        private void OnEnable()
        {
            _installer = (ModularAvatarMenuInstaller) target;

            FindMenus();
            FindMenuFolderCreators();
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
            
            VRCAvatarDescriptor commonAvatar = FindCommonAvatar();

            SerializedProperty installTargetTypeProperty = serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.InstallTargetType));
            EditorGUILayout.PropertyField(installTargetTypeProperty, new GUIContent("Install Target Type"));
            InstallTargetType installTargetType = (InstallTargetType)Enum.ToObject(typeof(InstallTargetType), installTargetTypeProperty.enumValueIndex);

            if (!installTargetTypeProperty.hasMultipleDifferentValues) 
            {
                string installTargetMenuPropertyName;
                Type installTargetObjectType;
                if (installTargetType == InstallTargetType.VRCExpressionMenu) 
                {
                    installTargetMenuPropertyName = nameof(ModularAvatarSubMenuCreator.installTargetMenu);
                    installTargetObjectType = typeof(VRCExpressionsMenu);
                } else 
                {
                    installTargetMenuPropertyName = nameof(ModularAvatarSubMenuCreator.installTargetCreator);
                    installTargetObjectType = typeof(ModularAvatarSubMenuCreator);
                    commonAvatar = null;
                }

                SerializedProperty installTargetProperty = this.serializedObject.FindProperty(installTargetMenuPropertyName);
                this.ShowMenuInstallerHelpBox(installTargetProperty, installTargetType);
                this.ShowInstallTargetPropertyField(installTargetProperty, commonAvatar, installTargetObjectType);

                var avatar = RuntimeUtil.FindAvatarInParents(_installer.transform);
                if (avatar != null && GUILayout.Button(G("menuinstall.selectmenu")))
                {
                    if (installTargetType == InstallTargetType.VRCExpressionMenu) 
                    {
                        AvMenuTreeViewWindow.Show(avatar, menu => 
                        {
                            installTargetProperty.objectReferenceValue = menu;
                            serializedObject.ApplyModifiedProperties();
                        });
                    } else {
                        AvMenuFolderCreatorTreeViewWindow.Show(avatar, null, creator => 
                        {
                            installTargetProperty.objectReferenceValue = creator;
                            serializedObject.ApplyModifiedProperties();
                        });
                    }
                }
            
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
        
        private void ShowMenuInstallerHelpBox(SerializedProperty installTargetProperty, InstallTargetType installTargetType)
        {
            if (installTargetProperty.hasMultipleDifferentValues) return;
            bool isEnabled = targets.Length != 1 || this._installer.enabled;

            if (installTargetProperty.objectReferenceValue == null)
            {
                if (!isEnabled) return;
                EditorGUILayout.HelpBox(S("menuinstall.help.hint_set_menu"), MessageType.Info);
            } 
            else 
            {
                VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(this._installer.transform);
                switch (installTargetType) 
                {
                    case InstallTargetType.VRCExpressionMenu:
                        if (!this.IsMenuReachable(avatar, (VRCExpressionsMenu)installTargetProperty.objectReferenceValue)) 
                        {
                            EditorGUILayout.HelpBox(Localization.S("menuinstall.help.hint_bad_menu"), MessageType.Error);
                        }
                        break;
                    case InstallTargetType.FolderCreator:
                        if (!this.IsMenuReachable(avatar, (ModularAvatarSubMenuCreator)installTargetProperty.objectReferenceValue, new HashSet<ModularAvatarSubMenuCreator>())) 
                        {
                            EditorGUILayout.HelpBox("選択されたメニューフォルダからアバターまでのパスが見つかりません。", MessageType.Error);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(installTargetType), installTargetType, null);
                }
            }
        }

        private void ShowInstallTargetPropertyField(SerializedProperty installTargetProperty, VRCAvatarDescriptor avatar, Type propertyType) 
        {
            Object displayValue = installTargetProperty.objectReferenceValue;
            if (!installTargetProperty.hasMultipleDifferentValues && avatar != null) 
            {
                if (displayValue == null) displayValue = avatar.expressionsMenu;
            }
            
            EditorGUI.BeginChangeCheck();
            Object newValue = EditorGUILayout.ObjectField(G("menuinstall.installto"), displayValue, propertyType, 
                propertyType == typeof(ModularAvatarSubMenuCreator));
            if (EditorGUI.EndChangeCheck()) 
            {
                installTargetProperty.objectReferenceValue = newValue;
            }
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

        private void FindMenuFolderCreators() 
        {
            if (this.targets.Length > 1) 
            {
                this._menuFolderCreators = null;
                return;
            }

            this._menuFolderCreators = new HashSet<ModularAvatarSubMenuCreator>();
            VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(this._installer.transform);
            if (avatar == null) return;
            foreach (ModularAvatarSubMenuCreator creator in avatar.gameObject.GetComponentsInChildren<ModularAvatarSubMenuCreator>()) 
            {
                this._menuFolderCreators.Add(creator);
            }
        }

        private bool IsMenuReachable(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu)
        {
            return _avatarMenus == null || _avatarMenus.Contains(menu);
        }
        
        private bool IsMenuReachable(VRCAvatarDescriptor avatar, ModularAvatarSubMenuCreator creator, HashSet<ModularAvatarSubMenuCreator> session) 
        {
            if (avatar == null) return true;
            if (this._menuFolderCreators == null) return true;

            if (session.Contains(creator)) return false;
            if (!this._menuFolderCreators.Contains(creator)) return false;

            if (!creator.enabled) return false;
            session.Add(creator);
            switch (creator.installTargetType) 
            {
                case InstallTargetType.VRCExpressionMenu:
                    return creator.installTargetMenu == null || this.IsMenuReachable(avatar, creator.installTargetMenu);
                case InstallTargetType.FolderCreator:
                    return creator.installTargetCreator == null || this.IsMenuReachable(avatar, creator.installTargetCreator, session);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static ValidateExpressionMenuIconResult ValidateExpressionMenuIcon(VRCExpressionsMenu menu) {
            if (menu == null) return ValidateExpressionMenuIconResult.Success;

            foreach (VRCExpressionsMenu.Control control in menu.controls) {
                // Control
                ValidateExpressionMenuIconResult result = Util.ValidateExpressionMenuIcon(control.icon);
                if (result != ValidateExpressionMenuIconResult.Success) return result;

                // Labels
                foreach (VRCExpressionsMenu.Control.Label label in control.labels) {
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