﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.menu;
using nadena.dev.modular_avatar.core.menu;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;
using static nadena.dev.modular_avatar.core.editor.Util;
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

        private Dictionary<VRCExpressionsMenu, List<ModularAvatarMenuInstaller>> _menuInstallersMap;

        private void OnEnable()
        {
            _installer = (ModularAvatarMenuInstaller) target;

            FindMenus();
            FindMenuInstallers();

            VRCAvatarDescriptor commonAvatar = FindCommonAvatar();
        }

        private long _cacheSeq = -1;
        private ImmutableList<object> _cachedTargets = null;

        // Interpretation:
        //  <empty> : Inconsistent install targets
        //  List of [null]: Install to root
        //  List of [VRCExpMenu]: Install to expressions menu
        //  List of [InstallTarget]: Install to single install target
        //  List of [InstallTarget, InstallTarget ...]: Install to multiple install targets
        private ImmutableList<object> InstallTargets
        {
            get
            {
                if (VirtualMenu.CacheSequence == _cacheSeq && _cachedTargets != null) return _cachedTargets;

                List<ImmutableList<object>> perTarget = new List<ImmutableList<object>>();

                var commonAvatar = FindCommonAvatar();
                if (commonAvatar == null)
                {
                    _cacheSeq = VirtualMenu.CacheSequence;
                    _cachedTargets = ImmutableList<object>.Empty;
                    return _cachedTargets;
                }

                var virtualMenu = VirtualMenu.ForAvatar(commonAvatar);

                foreach (var target in targets)
                {
                    var installer = (ModularAvatarMenuInstaller) target;

                    var installTargets = virtualMenu.GetInstallTargetsForInstaller(installer)
                        .Select(o => (object) o).ToImmutableList();
                    if (installTargets.Any())
                    {
                        perTarget.Add(installTargets);
                    }
                    else
                    {
                        perTarget.Add(ImmutableList<object>.Empty.Add(installer.installTargetMenu));
                    }
                }

                for (int i = 1; i < perTarget.Count; i++)
                {
                    if (perTarget[0].Count != perTarget[i].Count ||
                        perTarget[0].Zip(perTarget[i], (a, b) => (Resolve(a) != Resolve(b))).Any(differs => differs))
                    {
                        perTarget.Clear();
                        perTarget.Add(ImmutableList<object>.Empty);
                        break;
                    }
                }

                _cacheSeq = VirtualMenu.CacheSequence;
                _cachedTargets = perTarget[0];
                return _cachedTargets;

                object Resolve(object p0)
                {
                    if (p0 is ModularAvatarMenuInstallTarget target && target != null) return target.transform.parent;
                    return p0;
                }
            }
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

            if (InstallTargets.Count == 0)
            {
                // TODO - show warning for inconsistent targets?
            }
            else if (InstallTargets.Count > 0)
            {
                if (InstallTargets.Count == 1)
                {
                    if (InstallTargets[0] == null)
                    {
                        if (isEnabled)
                        {
                            EditorGUILayout.HelpBox(S("menuinstall.help.hint_set_menu"), MessageType.Info);
                        }
                    }
                    else if (InstallTargets[0] is VRCExpressionsMenu menu
                             && !IsMenuReachable(RuntimeUtil.FindAvatarInParents(((Component) target).transform), menu))
                    {
                        EditorGUILayout.HelpBox(S("menuinstall.help.hint_bad_menu"), MessageType.Error);
                    }
                }

                if (InstallTargets.Count == 1 && (InstallTargets[0] is VRCExpressionsMenu || InstallTargets[0] == null))
                {
                    var displayValue = installTo.objectReferenceValue;
                    if (displayValue == null) displayValue = commonAvatar.expressionsMenu;

                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUILayout.ObjectField(G("menuinstall.installto"), displayValue,
                        typeof(VRCExpressionsMenu), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        installTo.objectReferenceValue = newValue;
                        _cacheSeq = -1;
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        foreach (var target in InstallTargets)
                        {
                            if (target is VRCExpressionsMenu menu)
                            {
                                EditorGUILayout.ObjectField(G("menuinstall.installto"), menu,
                                    typeof(VRCExpressionsMenu), true);
                            }
                            else if (target is ModularAvatarMenuInstallTarget t)
                            {
                                EditorGUILayout.ObjectField(G("menuinstall.installto"), t.transform.parent.gameObject,
                                    typeof(GameObject), true);
                            }
                        }
                    }
                }

                var avatar = commonAvatar;
                if (avatar != null && InstallTargets.Count == 1 && GUILayout.Button(G("menuinstall.selectmenu")))
                {
                    AvMenuTreeViewWindow.Show(avatar, _installer, menu =>
                    {
                        if (InstallTargets.Count != 1 || menu == InstallTargets[0]) return;

                        if (InstallTargets[0] is ModularAvatarMenuInstallTarget oldTarget && oldTarget != null)
                        {
                            DestroyInstallTargets();
                        }

                        if (menu is VRCExpressionsMenu expMenu)
                        {
                            if (expMenu == avatar.expressionsMenu) installTo.objectReferenceValue = null;
                            else installTo.objectReferenceValue = expMenu;
                        }
                        else if (menu is RootMenu)
                        {
                            installTo.objectReferenceValue = null;
                        }
                        else if (menu is ModularAvatarMenuItem item)
                        {
                            installTo.objectReferenceValue = null;

                            foreach (var target in targets)
                            {
                                var installer = (ModularAvatarMenuInstaller) target;
                                var child = new GameObject();
                                Undo.RegisterCreatedObjectUndo(child, "Set install target");
                                child.transform.SetParent(item.transform, false);
                                child.name = installer.gameObject.name;

                                var targetComponent = child.AddComponent<ModularAvatarMenuInstallTarget>();
                                targetComponent.installer = installer;
                            }
                        }

                        serializedObject.ApplyModifiedProperties();
                        VirtualMenu.InvalidateCaches();
                        Repaint();
                    });
                }
            }

            if (targets.Length == 1)
            {
                /* TODO
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
                */
            }

            bool inconsistentSources = false;
            MenuSource menuSource = null;
            bool first = true;
            foreach (var target in targets)
            {
                var component = (ModularAvatarMenuInstaller) target;
                var componentSource = component.GetComponent<MenuSource>();
                if (componentSource != null)
                {
                    if (menuSource == null && first)
                    {
                        menuSource = componentSource;
                    }
                    else
                    {
                        inconsistentSources = true;
                    }
                }
            }

            if (menuSource != null)
            {
                // TODO localize
                EditorGUILayout.HelpBox("Menu contents provided by " + menuSource.GetType() + " component",
                    MessageType.Info);
            }

            if (!inconsistentSources)
            {
                _devFoldout = EditorGUILayout.Foldout(_devFoldout, G("menuinstall.devoptions"));
                if (_devFoldout)
                {
                    SerializedProperty menuToAppendProperty =
                        serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.menuToAppend));
                    if (!menuToAppendProperty.hasMultipleDifferentValues)
                    {
                        switch (ValidateExpressionMenuIcon(
                                    (VRCExpressionsMenu) menuToAppendProperty.objectReferenceValue))
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
                    }

                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        menuToAppendProperty, new GUIContent(G("menuinstall.srcmenu")));
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();

            Localization.ShowLanguageUI();
        }

        private void DestroyInstallTargets()
        {
            VirtualMenu menu = VirtualMenu.ForAvatar(FindCommonAvatar());

            foreach (var t in targets)
            {
                foreach (var oldTarget in menu.GetInstallTargetsForInstaller((ModularAvatarMenuInstaller) t))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(oldTarget))
                    {
                        Undo.RecordObject(oldTarget, "Change menu install target");
                        oldTarget.installer = null;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(oldTarget);
                    }
                    else
                    {
                        if (oldTarget.transform.childCount == 0 &&
                            oldTarget.GetComponents(typeof(Component)).Length == 2)
                        {
                            Undo.DestroyObjectImmediate(oldTarget.gameObject);
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(oldTarget);
                        }
                    }
                }
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

        private void FindMenuInstallers()
        {
            if (targets.Length > 1)
            {
                _menuInstallersMap = null;
                return;
            }

            _menuInstallersMap = new Dictionary<VRCExpressionsMenu, List<ModularAvatarMenuInstaller>>();
            var avatar = RuntimeUtil.FindAvatarInParents(((Component) target).transform);
            if (avatar == null) return;
            var menuInstallers = avatar.GetComponentsInChildren<ModularAvatarMenuInstaller>(true)
                .Where(menuInstaller => menuInstaller.enabled && menuInstaller.menuToAppend != null);
            foreach (ModularAvatarMenuInstaller menuInstaller in menuInstallers)
            {
                if (menuInstaller == target) continue;
                var visitedMenus = new HashSet<VRCExpressionsMenu>();
                var queue = new Queue<VRCExpressionsMenu>();
                queue.Enqueue(menuInstaller.menuToAppend);

                while (queue.Count > 0)
                {
                    VRCExpressionsMenu parent = queue.Dequeue();
                    var controls = parent.controls.Where(control =>
                        control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null);
                    foreach (VRCExpressionsMenu.Control control in controls)
                    {
                        // Do not filter in LINQ to avoid closure allocation
                        if (visitedMenus.Contains(control.subMenu)) continue;
                        if (!_menuInstallersMap.TryGetValue(control.subMenu,
                                out List<ModularAvatarMenuInstaller> fromInstallers))
                        {
                            fromInstallers = new List<ModularAvatarMenuInstaller>();
                            _menuInstallersMap[control.subMenu] = fromInstallers;
                        }

                        fromInstallers.Add(menuInstaller);
                        visitedMenus.Add(control.subMenu);
                        queue.Enqueue(control.subMenu);
                    }
                }
            }
        }

        private bool IsMenuReachable(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu,
            HashSet<ModularAvatarMenuInstaller> visitedInstaller = null)
        {
            if (_avatarMenus == null || _avatarMenus.Contains(menu)) return true;

            if (_menuInstallersMap == null) return true;
            if (visitedInstaller == null)
                visitedInstaller = new HashSet<ModularAvatarMenuInstaller> {(ModularAvatarMenuInstaller) target};

            if (!_menuInstallersMap.TryGetValue(menu, out List<ModularAvatarMenuInstaller> installers)) return false;
            foreach (ModularAvatarMenuInstaller installer in installers)
            {
                // Root is always reachable if installTargetMenu is null
                if (installer.installTargetMenu == null) return true;
                // Even in a circular structure, it may be possible to reach root by another path.
                if (visitedInstaller.Contains(installer)) continue;
                visitedInstaller.Add(installer);
                if (IsMenuReachable(avatar, installer.installTargetMenu, visitedInstaller))
                {
                    return true;
                }
            }

            return false;
        }

        private static ValidateExpressionMenuIconResult ValidateExpressionMenuIcon(VRCExpressionsMenu menu,
            HashSet<VRCExpressionsMenu> visitedMenus = null)
        {
            if (menu == null) return ValidateExpressionMenuIconResult.Success;
            if (visitedMenus == null) visitedMenus = new HashSet<VRCExpressionsMenu>();
            if (visitedMenus.Contains(menu)) return ValidateExpressionMenuIconResult.Success;
            visitedMenus.Add(menu);

            foreach (VRCExpressionsMenu.Control control in menu.controls)
            {
                // Control
                ValidateExpressionMenuIconResult result = Util.ValidateExpressionMenuIcon(control.icon);
                if (result != ValidateExpressionMenuIconResult.Success) return result;

                // Labels
                if (control.labels != null)
                {
                    foreach (VRCExpressionsMenu.Control.Label label in control.labels)
                    {
                        ValidateExpressionMenuIconResult labelResult = Util.ValidateExpressionMenuIcon(label.icon);
                        if (labelResult != ValidateExpressionMenuIconResult.Success) return labelResult;
                    }
                }

                // SubMenu
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                ValidateExpressionMenuIconResult subMenuResult =
                    ValidateExpressionMenuIcon(control.subMenu, visitedMenus);
                if (subMenuResult != ValidateExpressionMenuIconResult.Success) return subMenuResult;
            }

            return ValidateExpressionMenuIconResult.Success;
        }
    }
}