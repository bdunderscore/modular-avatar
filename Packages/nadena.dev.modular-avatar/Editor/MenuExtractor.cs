using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuExtractor
    {
        private const int PRIORITY = 49;

        [MenuItem("GameObject/[Modular Avatar] Extract menu", false, PRIORITY)]
        static void ExtractMenu(MenuCommand menuCommand)
        {
            if (!(menuCommand.context is GameObject gameObj)) return;
            var avatar = gameObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null || avatar.expressionsMenu == null) return;

            VRCExpressionsMenu.Control fakeControl = new VRCExpressionsMenu.Control()
            {
                subMenu = avatar.expressionsMenu,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = "Avatar Menu"
            };
            var rootMenu = ConvertSubmenu(gameObj, fakeControl, new Dictionary<VRCExpressionsMenu, MenuSource>());
            Undo.RecordObject(avatar, "Convert menu");
            avatar.expressionsMenu = null;

            rootMenu.gameObject.AddComponent<ModularAvatarMenuInstaller>();
        }

        private static MenuSource ConvertSubmenu(
            GameObject parentObj,
            VRCExpressionsMenu.Control sourceControl,
            Dictionary<VRCExpressionsMenu, MenuSource> convertedMenus
        )
        {
            var itemObj = new GameObject();
            itemObj.name = string.IsNullOrEmpty(sourceControl.name) ? " " : sourceControl.name;
            Undo.RegisterCreatedObjectUndo(itemObj, "Convert menu");
            itemObj.transform.SetParent(parentObj.transform);
            itemObj.transform.localPosition = Vector3.zero;
            itemObj.transform.localRotation = Quaternion.identity;
            itemObj.transform.localScale = Vector3.one;

            var menuItem = itemObj.AddComponent<MAMenuItem>();
            menuItem.Control = sourceControl;

            if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                if (convertedMenus.TryGetValue(sourceControl.subMenu, out var otherSource))
                {
                    menuItem.MenuSource = SubmenuSource.OtherMenuItem;
                    menuItem.menuSource_otherSource = otherSource;
                }
                else
                {
                    convertedMenus[sourceControl.subMenu] = menuItem;

                    menuItem.MenuSource = SubmenuSource.Children;

                    if (sourceControl.subMenu.controls != null)
                    {
                        foreach (var childControl in sourceControl.subMenu.controls)
                        {
                            ConvertSubmenu(itemObj, childControl, convertedMenus);
                        }
                    }
                }
            }

            return menuItem;
        }
    }
}