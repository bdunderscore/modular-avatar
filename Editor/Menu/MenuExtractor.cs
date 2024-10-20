#if MA_VRCSDK3_AVATARS

#region

using System;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuExtractor
    {
        [MenuItem(UnityMenuItems.GameObject_ExtractMenu, false, UnityMenuItems.GameObject_ExtractMenuOrder)]
        static void ExtractMenu(MenuCommand menuCommand)
        {
            if (!(menuCommand.context is GameObject gameObj)) return;
            var avatar = gameObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null || avatar.expressionsMenu == null || avatar.expressionsMenu.controls.Count == 0) return;

            var parent = ExtractSingleLayerMenu(avatar.expressionsMenu, gameObj, "Avatar Menu");
            parent.AddComponent<ModularAvatarMenuInstaller>();
            parent.AddComponent<ModularAvatarMenuGroup>();

            // The VRCSDK requires that an expressions menu asset be provided if any parameters are defined.
            // We can't just remove the asset, so we'll replace it with a dummy asset. However, to avoid users
            // accidentally overwriting files in Packages, we'll place this dummy asset next to where the original
            // asset was (or in the Assets root, if the original asset was in Packages).
            Undo.RecordObject(avatar, "Extract menu");

            var assetPath = AssetDatabase.GetAssetPath(avatar.expressionsMenu);
            var dummyAssetPathBase = assetPath.Replace(".asset", " placeholder");
            if (dummyAssetPathBase.StartsWith("Packages" + Path.DirectorySeparatorChar))
            {
                var filename = Path.GetFileName(dummyAssetPathBase);
                dummyAssetPathBase = Path.Combine("Assets", filename);
            }

            // Check that a similarly-named file doesn't already exist
            int i = 0;
            do
            {
                var fullPath = dummyAssetPathBase + (i > 0 ? " " + i : "") + ".asset";
                if (File.Exists(fullPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(fullPath);
                    if (asset != null && asset.controls.Count == 0)
                    {
                        avatar.expressionsMenu = asset;
                        break;
                    }
                }
                else if (!File.Exists(fullPath))
                {
                    var dummyAsset = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    AssetDatabase.CreateAsset(dummyAsset, fullPath);
                    avatar.expressionsMenu = dummyAsset;

                    break;
                }

                i++;
            } while (true);

            EditorUtility.SetDirty(avatar);
            PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);
        }

        /// <summary>
        /// Extracts a single expressions menu asset to Menu Item components.
        /// </summary>
        /// <param name="menu">The menu to extract</param>
        /// <param name="parent">The parent object to use</param>
        /// <param name="containerName">The name of a gameobject to place between the parent and menu item objects,
        /// or null to skip</param>
        /// <returns>the direct parent of the generated menu items</returns>
        internal static GameObject ExtractSingleLayerMenu(
            VRCExpressionsMenu menu,
            GameObject parent,
            string containerName = null)
        {
            if (containerName != null)
            {
                var container = new GameObject();
                container.name = containerName;
                container.transform.SetParent(parent.transform, false);
                parent = container;
                Undo.RegisterCreatedObjectUndo(container, "Convert menu");
            }

            foreach (var control in menu.controls)
            {
                var itemObj = new GameObject();
                itemObj.name = string.IsNullOrEmpty(control.name) ? " " : control.name;
                Undo.RegisterCreatedObjectUndo(itemObj, "Convert menu");
                itemObj.transform.SetParent(parent.transform, false);

                var menuItem = itemObj.AddComponent<ModularAvatarMenuItem>();
                ControlToMenuItem(menuItem, control);
            }

            return parent;
        }

        internal static void ControlToMenuItem(ModularAvatarMenuItem menuItem, VRCExpressionsMenu.Control control)
        {
            menuItem.Control = CloneControl(control);
            if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                menuItem.MenuSource = SubmenuSource.MenuAsset;
            }
        }

        internal static VRCExpressionsMenu.Control CloneControl(VRCExpressionsMenu.Control c)
        {
            var type = c.type != 0 ? c.type : VRCExpressionsMenu.Control.ControlType.Button;
            
            return new VRCExpressionsMenu.Control()
            {
                type = type,
                name = c.name,
                icon = c.icon,
                parameter = new VRCExpressionsMenu.Control.Parameter() { name = c.parameter?.name },
                subMenu = c.subMenu,
                subParameters = c.subParameters?.Select(p =>
                        new VRCExpressionsMenu.Control.Parameter() { name = p?.name })
                    ?.ToArray() ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>(),
                labels = c.labels?.ToArray() ?? Array.Empty<VRCExpressionsMenu.Control.Label>(),
                style = c.style,
                value = c.value,
            };
        }
    }
}

#endif