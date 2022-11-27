using System;
using System.Collections.Generic;
using System.Linq;
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

        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _clonedMenus;
        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _installTargets;

        private VRCExpressionsMenu _rootMenu;

        public void OnPreprocessAvatar(GameObject avatarRoot)
        {
            var menuInstallers = avatarRoot.GetComponentsInChildren<ModularAvatarMenuInstaller>(true)
                .Where(c => c.enabled)
                .ToArray();
            if (menuInstallers.Length == 0) return;

            _clonedMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            var avatar = avatarRoot.GetComponent<VRCAvatarDescriptor>();

            if (avatar.expressionsMenu == null)
            {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(menu, Util.GenerateAssetPath());
                avatar.expressionsMenu = menu;
            }

            _rootMenu = avatar.expressionsMenu;
            avatar.expressionsMenu = CloneMenu(avatar.expressionsMenu);
            _installTargets = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>(_clonedMenus);

            foreach (var install in menuInstallers)
            {
                InstallMenu(install);
            }
        }

        private void InstallMenu(ModularAvatarMenuInstaller installer)
        {
            if (!installer.enabled) return;

            if (installer.installTargetMenu == null)
            {
                installer.installTargetMenu = _rootMenu;
            }

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
                    subMenu = newMenu,
                    parameter = new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = ""
                    },
                    subParameters = Array.Empty<VRCExpressionsMenu.Control.Parameter>(),
                    icon = _moreIcon,
                    labels = Array.Empty<VRCExpressionsMenu.Control.Label>()
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