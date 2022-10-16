﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.modular_avatar.core.editor
{
    public class MenuInstallHook
    {
        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _clonedMenus;
        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _installTargets;

        public void OnPreprocessAvatar(GameObject avatarRoot)
        {
            var menuInstallers = avatarRoot.GetComponentsInChildren<ModularAvatarMenuInstaller>(true);
            if (menuInstallers.Length == 0) return;

            _clonedMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            var avatar = avatarRoot.GetComponent<VRCAvatarDescriptor>();

            avatar.expressionsMenu = CloneMenu(avatar.expressionsMenu);
            _installTargets = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>(_clonedMenus);

            foreach (var install in menuInstallers)
            {
                InstallMenu(install);
            }
        }

        private void InstallMenu(ModularAvatarMenuInstaller installer)
        {
            if (installer.installTargetMenu == null || installer.menuToAppend == null) return;
            if (!_installTargets.TryGetValue(installer.installTargetMenu, out var targetMenu)) return;
            if (_installTargets.ContainsKey(installer.menuToAppend)) return;

            targetMenu.controls.AddRange(installer.menuToAppend.controls);

            while (targetMenu.controls.Count > VRCExpressionsMenu.MAX_CONTROLS)
            {
                // Split target menu
                var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(newMenu, Util.GenerateAssetPath());
                var keepCount = VRCExpressionsMenu.MAX_CONTROLS - 1;
                newMenu.controls.AddRange(targetMenu.controls.Skip(keepCount));
                targetMenu.controls.RemoveRange(keepCount,
                    targetMenu.controls.Count - keepCount
                );

                targetMenu.controls.Add(new VRCExpressionsMenu.Control()
                {
                    name = "More",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = newMenu
                });

                _installTargets[installer.installTargetMenu] = newMenu;
                targetMenu = newMenu;
            }
        }

        private VRCExpressionsMenu CloneMenu(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            if (_clonedMenus.TryGetValue(menu, out var newMenu)) return newMenu;
            newMenu = Object.Instantiate(menu);
            AssetDatabase.CreateAsset(newMenu, Util.GenerateAssetPath());
            _clonedMenus[menu] = newMenu;

            foreach (var control in newMenu.controls)
            {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    control.subMenu = CloneMenu(control.subMenu);
                }
            }

            return newMenu;
        }
    }
}