#if MA_VRCSDK3_AVATARS

using System;
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

        private MenuPreviewGUI _previewGUI;

        private HashSet<VRCExpressionsMenu> _avatarMenus;
        private VirtualMenu _virtualMenuCache;

        private Dictionary<VRCExpressionsMenu, List<ModularAvatarMenuInstaller>> _menuInstallersMap;

        private static Editor _cachedEditor;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            ModularAvatarMenuInstaller._openSelectMenu = OpenSelectInstallTargetMenu;
        }

        private static void OpenSelectInstallTargetMenu(ModularAvatarMenuInstaller installer)
        {
            CreateCachedEditor(installer, typeof(MenuInstallerEditor), ref _cachedEditor);

            var editor = (MenuInstallerEditor)_cachedEditor;
            editor.OnEnable();

            var serializedObject = editor.serializedObject;
            var installTo = serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.installTargetMenu));

            var root = editor.FindCommonAvatar();

            editor.OpenSelectMenu(root, installTo);
        }
        
        private void OnEnable()
        {
            _installer = (ModularAvatarMenuInstaller) target;
            _previewGUI = new MenuPreviewGUI(Repaint);

            FindMenus();
            FindMenuInstallers();

            VRCAvatarDescriptor commonAvatar = FindCommonAvatar();
        }

        private long _cacheSeq = -1;
        private ImmutableList<object> _cachedTargets = null;

        private void CacheMenu()
        {
            if (VirtualMenu.CacheSequence == _cacheSeq && _cachedTargets != null && _virtualMenuCache != null) return;


            List<ImmutableList<object>> perTarget = new List<ImmutableList<object>>();

            var commonAvatar = FindCommonAvatar();
            if (commonAvatar == null)
            {
                _cacheSeq = VirtualMenu.CacheSequence;
                _cachedTargets = ImmutableList<object>.Empty;
                _virtualMenuCache = null;
                return;
            }

            _virtualMenuCache = VirtualMenu.ForAvatar(commonAvatar);

            foreach (var target in targets)
            {
                var installer = (ModularAvatarMenuInstaller) target;

                var installTargets = _virtualMenuCache.GetInstallTargetsForInstaller(installer)
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

            object Resolve(object p0)
            {
                if (p0 is ModularAvatarMenuInstallTarget target && target != null) return target.transform.parent;
                return p0;
            }
        }

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
                CacheMenu();

                return _cachedTargets;
            }
        }

        private VirtualMenu _virtualMenu
        {
            get
            {
                CacheMenu();
                return _virtualMenuCache;
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

                if (commonAvatar != null && InstallTargets.Count == 1 && (InstallTargets[0] is VRCExpressionsMenu || InstallTargets[0] == null))
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
                    OpenSelectMenu(avatar, installTo);
                }
            }

            if (targets.Length == 1)
            {
                _menuFoldout = EditorGUILayout.Foldout(_menuFoldout, G("menuinstall.showcontents"));
                if (_menuFoldout)
                {
                    _previewGUI.DoGUI((ModularAvatarMenuInstaller) target);
                }
            }

            if (targets.Any(t =>
                {
                    var installer = (ModularAvatarMenuInstaller) t;
                    return installer.GetComponent<MenuSource>() == null && installer.menuToAppend != null;
                }))
            {
                if (GUILayout.Button("Extract menu to objects"))
                {
                    ExtractMenu();
                }
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

            ShowLanguageUI();
        }

        private void OpenSelectMenu(VRCAvatarDescriptor avatar, SerializedProperty installTo)
        {
            AvMenuTreeViewWindow.Show(avatar, _installer, menu =>
            {
                if (InstallTargets.Count != 1 || menu == InstallTargets[0]) return;

                if (InstallTargets[0] is ModularAvatarMenuInstallTarget oldTarget && oldTarget != null)
                {
                    DestroyInstallTargets();
                }

                if (menu is ValueTuple<object, object> vt) // TODO: This should be a named type...
                {
                    // Menu, ContextCallback
                    menu = vt.Item1;
                }

                if (menu is ModularAvatarMenuItem item)
                {
                    if (item.MenuSource == SubmenuSource.MenuAsset)
                    {
                        menu = item.Control.subMenu;
                    }
                    else
                    {
                        var menuParent = item.menuSource_otherObjectChildren != null
                            ? item.menuSource_otherObjectChildren
                            : item.gameObject;

                        menu = new MenuNodesUnder(menuParent);
                    }
                }
                else if (menu is ModularAvatarMenuGroup group)
                {
                    if (group.targetObject != null) menu = new MenuNodesUnder(group.targetObject);
                    else menu = new MenuNodesUnder(group.gameObject);
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
                else if (menu is MenuNodesUnder nodesUnder)
                {
                    installTo.objectReferenceValue = null;

                    foreach (var target in targets.Cast<Component>().OrderBy(ObjectHierarchyOrder))
                    {
                        var installer = (ModularAvatarMenuInstaller)target;
                        var child = new GameObject();
                        Undo.RegisterCreatedObjectUndo(child, "Set install target");
                        child.transform.SetParent(nodesUnder.root.transform, false);
                        child.name = installer.gameObject.name;

                        var targetComponent = child.AddComponent<ModularAvatarMenuInstallTarget>();
                        targetComponent.installer = installer;

                        EditorGUIUtility.PingObject(child);
                    }
                }

                serializedObject.ApplyModifiedProperties();
                VirtualMenu.InvalidateCaches();
                Repaint();
            });
        }

        private string ObjectHierarchyOrder(Component arg)
        {
            var list = new List<int>();
            var t = arg.transform;
            while (t != null)
            {
                list.Add(t.GetSiblingIndex());
                t = t.parent;
            }

            list.Reverse();
            return string.Join("", list.Select(n => (char) n));
        }

        private void ExtractMenu()
        {
            serializedObject.ApplyModifiedProperties();

            foreach (var t in targets)
            {
                var installer = (ModularAvatarMenuInstaller) t;
                if (installer.GetComponent<MenuSource>() != null || installer.menuToAppend == null) continue;

                var menu = installer.menuToAppend;
                if (menu.controls.Count == 0)
                {
                    continue;
                }

                Undo.RecordObject(installer, "Extract menu");

                if (menu.controls.Count == 1)
                {
                    // Attach control directly to the installer
                    var item = installer.gameObject.AddComponent<ModularAvatarMenuItem>();
                    Undo.RegisterCreatedObjectUndo(item, "Extract menu");
                    MenuExtractor.ControlToMenuItem(item, menu.controls[0]);
                }
                else
                {
                    // Use a menu group and attach items on a child
                    var group = installer.gameObject.AddComponent<ModularAvatarMenuGroup>();
                    var menuRoot = new GameObject();
                    menuRoot.name = "Menu";

                    group.targetObject = menuRoot;
                    
                    Undo.RegisterCreatedObjectUndo(menuRoot, "Extract menu");
                    menuRoot.transform.SetParent(group.transform, false);
                    foreach (var control in menu.controls)
                    {
                        var itemObject = new GameObject();
                        itemObject.gameObject.name = control.name;
                        Undo.RegisterCreatedObjectUndo(itemObject, "Extract menu");
                        itemObject.transform.SetParent(menuRoot.transform, false);
                        var item = itemObject.AddComponent<ModularAvatarMenuItem>();
                        MenuExtractor.ControlToMenuItem(item, control);
                    }
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(installer);
                EditorUtility.SetDirty(installer);
            }
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
                    if (subMenu.type == VRCExpressionsMenu.Control.ControlType.SubMenu && subMenu.subMenu != null)
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

        private bool IsMenuReachable(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu)
        {
            var virtualMenu = VirtualMenu.ForAvatar(avatar);

            return virtualMenu.ContainsMenu(menu);
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

#endif