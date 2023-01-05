using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;


namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuInstallHook
    {
        private static Texture2D _moreIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Packages/nadena.dev.modular-avatar/Runtime/Icons/Icon_More_A.png"
        );

        private BuildContext _context;

        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _clonedMenus;


        private VRCExpressionsMenu _rootMenu;

        private MenuTree _menuTree;
        private Stack<ModularAvatarMenuInstaller> _visitedInstallerStack;

        public void OnPreprocessAvatar(GameObject avatarRoot, BuildContext context)
        {
            _context = context;

            ModularAvatarMenuInstaller[] menuInstallers = avatarRoot
                .GetComponentsInChildren<ModularAvatarMenuInstaller>(true)
                .Where(menuInstaller => menuInstaller.enabled)
                .ToArray();
            if (menuInstallers.Length == 0) return;


            _clonedMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            _visitedInstallerStack = new Stack<ModularAvatarMenuInstaller>();

            VRCAvatarDescriptor avatar = avatarRoot.GetComponent<VRCAvatarDescriptor>();

            if (avatar.expressionsMenu == null)
            {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                _context.SaveAsset(menu);
                avatar.expressionsMenu = menu;
                _clonedMenus[menu] = menu;
            }

            _rootMenu = avatar.expressionsMenu;
            _menuTree = new MenuTree(avatar);
            _menuTree.TraverseAvatarMenu();

            avatar.expressionsMenu = CloneMenu(avatar.expressionsMenu);

            foreach (ModularAvatarMenuInstaller installer in menuInstallers)
            {
                _menuTree.TraverseMenuInstaller(installer);
            }

            foreach (MenuTree.ChildElement childElement in _menuTree.GetChildInstallers(null))
            {
                InstallMenu(childElement.installer);
            }
        }

        private void InstallMenu(ModularAvatarMenuInstaller installer, VRCExpressionsMenu installTarget = null)
        {
            if (!installer.enabled) return;

            if (installer.installTargetMenu == null)
            {
                installer.installTargetMenu = _rootMenu;
            }

            if (installTarget == null)
            {
                installTarget = installer.installTargetMenu;
            }

            if (installer.installTargetMenu == null || installer.menuToAppend == null) return;
            if (!_clonedMenus.TryGetValue(installTarget, out var targetMenu)) return;

            // Clone before appending to sanitize menu icons
            targetMenu.controls.AddRange(CloneMenu(installer.menuToAppend).controls);

            SplitMenu(installer, targetMenu);

            if (_visitedInstallerStack.Contains(installer)) return;
            _visitedInstallerStack.Push(installer);
            foreach (MenuTree.ChildElement childElement in _menuTree.GetChildInstallers(installer))
            {
                InstallMenu(childElement.installer, childElement.parent);
            }

            _visitedInstallerStack.Pop();
        }

        private void SplitMenu(ModularAvatarMenuInstaller installer, VRCExpressionsMenu targetMenu)
        {
            while (targetMenu.controls.Count > VRCExpressionsMenu.MAX_CONTROLS)
            {
                // Split target menu
                var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                _context.SaveAsset(newMenu);
                const int keepCount = VRCExpressionsMenu.MAX_CONTROLS - 1;
                newMenu.controls.AddRange(targetMenu.controls.Skip(keepCount));
                targetMenu.controls.RemoveRange(keepCount,
                    targetMenu.controls.Count - keepCount
                );

                targetMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = "More",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = newMenu,
                    parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = ""
                    },
                    subParameters = Array.Empty<VRCExpressionsMenu.Control.Parameter>(),
                    icon = _moreIcon,
                    labels = Array.Empty<VRCExpressionsMenu.Control.Label>()
                });

                _clonedMenus[installer.installTargetMenu] = newMenu;
                targetMenu = newMenu;
            }
        }

        private VRCExpressionsMenu CloneMenu(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            if (_clonedMenus.TryGetValue(menu, out var newMenu)) return newMenu;
            newMenu = Object.Instantiate(menu);
            _context.SaveAsset(newMenu);
            _clonedMenus[menu] = newMenu;

            foreach (var control in newMenu.controls)
            {
                if (Util.ValidateExpressionMenuIcon(control.icon) != Util.ValidateExpressionMenuIconResult.Success)
                    control.icon = null;

                for (int i = 0; i < control.labels.Length; i++)
                {
                    var label = control.labels[i];
                    var labelResult = Util.ValidateExpressionMenuIcon(label.icon);
                    if (labelResult != Util.ValidateExpressionMenuIconResult.Success)
                    {
                        label.icon = null;
                        control.labels[i] = label;
                    }
                }

                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    control.subMenu = CloneMenu(control.subMenu);
                }
            }

            return newMenu;
        }
    }
}