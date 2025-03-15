#if MA_VRCSDK3_AVATARS
using System;
using System.Linq;
using System.Collections.Generic;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ToggleCreatorShortcut
    {
        [MenuItem(UnityMenuItems.GameObject_CreateToggleForSelection, false, UnityMenuItems.GameObject_CreateToggleForSelectionOrder)]
        private static void CreateToggleForSelection()
        {
            var forSelection = true;

            var selections = Selection.objects.OfType<GameObject>();
            // Ignore GameObjects with submenu in the context of CreateToggleForSelection.
            selections = selections.Where(s => !TryGetChildrenSourceSubmenu(s, out var _));
            if (selections.Count() == 0) return;

            // Grouping according to parent
            var groups = new Dictionary<GameObject, HashSet<GameObject>>();
            foreach (var selected in selections)
            {
                var parent = selected.transform.parent?.gameObject;
                if (parent == null) continue;

                if (!groups.ContainsKey(parent))
                {
                    groups[parent] = new();
                }
                groups[parent].Add(selected);
            }

            foreach (var group in groups)
            {
                var parent = group.Key;
                var targets = group.Value;

                if (parent == null) continue;
                if (targets == null || targets.Count() == 0) continue;

                var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(parent.transform);
                if (avatarRoot == null) continue;
                
                var subMenuName = parent.name + " Toggles";

                // Try to find target submenu that should be the parent of toggles
                ModularAvatarMenuItem targetSubMenu = null;
                if (TryGetChildrenSourceSubmenu(parent, out var subMenu))
                {
                    // If parent has subMenu, use it as target submenu.
                    targetSubMenu = subMenu;
                }
                else
                {
                    // If parent hasn't subMenu, get submenus at the same level
                    var subMenus = new List<ModularAvatarMenuItem>();
                    foreach (Transform sibling in parent.transform)
                    {
                        if (TryGetChildrenSourceSubmenu(sibling.gameObject, out var m)) { subMenus.Add(m); }
                    }
                    // Filter to submenus with the same name
                    subMenus = subMenus.Where(m => m.gameObject.name == subMenuName).ToList();
                    // If only one submenu as target is found, use it as target submenu.
                    if (subMenus.Count() == 1) targetSubMenu = subMenus.First();
                }

                if (targetSubMenu != null) // If target SubMenu is found, add the toggles as children of it.
                {
                    parent = targetSubMenu.gameObject;
                    CreateToggleImpl(targets, parent, forSelection, createInstaller:false);
                }
                else
                {
                    if (targets.Count() > 1) // Create a submenu and add the toggles as children of it.
                    {
                        parent = CreateSubMenu(parent, subMenuName).gameObject;
                        CreateToggleImpl(targets, parent, forSelection, createInstaller:false);
                    }
                    else // Create a single toggle with installer.
                    {
                        var target = targets.First();
                        CreateToggleImpl(target, parent, forSelection, createInstaller:true);
                    }
                }
            }

            Selection.objects = null;
        }

        [MenuItem(UnityMenuItems.GameObject_CreateToggle, false, UnityMenuItems.GameObject_CreateToggleOrder)]
        private static void CreateToggle()
        {
            var selections = Selection.objects.OfType<GameObject>();
            if (selections.Count() == 0) return;

            foreach (var selected in selections)
            {
                var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selected.transform);
                if (avatarRoot == null) return;

                var parent = avatarRoot.gameObject;
                var createInstaller = true;

                if (TryGetChildrenSourceSubmenu(selected, out var _))
                {
                    parent = selected;
                    createInstaller = false;
                }

                CreateToggleImpl(selected, parent, createInstaller:createInstaller);
            }

            Selection.objects = null;
        }
        
        private static bool TryGetChildrenSourceSubmenu(GameObject target, out ModularAvatarMenuItem subMenu)
        {
            subMenu = null;
            try
            {
                var mami = target.GetComponent<ModularAvatarMenuItem>();
                if (mami?.Control?.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && mami.MenuSource == SubmenuSource.Children
                   )
                {
                    subMenu = mami;
                    return true;
                }
            }
            catch (MissingComponentException)
            {
                // ignore
            }
            return false;
        }
        
        private static ModularAvatarMenuItem CreateSubMenu(GameObject parent, string submenuname)
        {
            var submenu = new GameObject(submenuname);
            submenu.transform.SetParent(parent.transform);

            var mami = submenu.AddComponent<ModularAvatarMenuItem>();
            mami.InitSettings();
            mami.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = submenuname,
            };
            submenu.AddComponent<ModularAvatarMenuInstaller>();

            Selection.activeGameObject = submenu;
            EditorGUIUtility.PingObject(submenu);

            Undo.RegisterCreatedObjectUndo(submenu, "Create SubMenu");

            return mami;
        }

        private static void CreateToggleImpl(IEnumerable<GameObject> selections, GameObject parent, bool forSelection = false, bool createInstaller = true)
        {
            foreach (var selected in selections)
            {
                CreateToggleImpl(selected, parent, forSelection, createInstaller);
            }
        }
        
        private static void CreateToggleImpl(GameObject selected, GameObject parent, bool forSelection = false, bool createInstaller = true)
        {
            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selected.transform);
            if (avatarRoot == null) return;

            var suffix = selected.activeSelf ? "OFF" : "ON";
            var name = forSelection ? $"{selected.name} {suffix}" : "New Toggle";
            
            var toggle = new GameObject(name);
            
            var objToggle = toggle.AddComponent<ModularAvatarObjectToggle>();
            if (forSelection)
            {
                var path = RuntimeUtil.RelativePath(avatarRoot.gameObject, selected);
                objToggle.Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(){ referencePath = path },
                    Active = !selected.activeSelf
                });
            }


            toggle.transform.SetParent(parent.transform, false);

            var mami = toggle.AddComponent<ModularAvatarMenuItem>();
            mami.InitSettings();
            mami.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                name = name,
                value = 1,
            };
            
            if (createInstaller)
            {
                toggle.AddComponent<ModularAvatarMenuInstaller>();
            }

            Selection.activeGameObject = toggle;
            EditorGUIUtility.PingObject(objToggle);

            
            Undo.RegisterCreatedObjectUndo(toggle, "Create Toggle");
        }
    }
}
#endif