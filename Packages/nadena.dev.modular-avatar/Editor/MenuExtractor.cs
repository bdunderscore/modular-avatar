using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.menu;
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

            var parent = ExtractSingleLayerMenu(avatar.expressionsMenu, gameObj, "Avatar Menu");
            parent.AddComponent<ModularAvatarMenuInstaller>();

            // The VRCSDK requires that an expressions menu asset be provided if any parameters are defined.
            // We can't just remove the asset, so we'll replace it with a dummy asset. However, to avoid users
            // accidentally overwriting files in Packages, we'll place this dummy asset next to where the original
            // asset was (or in the Assets root, if the original asset was in Packages).
            Undo.RecordObject(avatar, "Extract menu");

            var assetPath = AssetDatabase.GetAssetPath(avatar.expressionsMenu);
            var dummyAssetPathBase = assetPath.Replace(".asset", " placeholder");
            if (dummyAssetPathBase.StartsWith("Packages" + System.IO.Path.DirectorySeparatorChar))
            {
                var filename = System.IO.Path.GetFileName(dummyAssetPathBase);
                dummyAssetPathBase = System.IO.Path.Combine("Assets", filename);
            }

            // Check that a similarly-named file doesn't already exist
            int i = 0;
            do
            {
                var fullPath = dummyAssetPathBase + (i > 0 ? " " + i : "") + ".asset";
                if (System.IO.File.Exists(fullPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(fullPath);
                    if (asset != null && asset.controls.Count == 0)
                    {
                        avatar.expressionsMenu = asset;
                        break;
                    }
                }
                else if (!System.IO.File.Exists(fullPath))
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
            return new VRCExpressionsMenu.Control()
            {
                type = c.type,
                name = c.name,
                icon = c.icon,
                parameter = new VRCExpressionsMenu.Control.Parameter() {name = c.parameter?.name},
                subMenu = c.subMenu,
                subParameters = c.subParameters?.Select(p =>
                        new VRCExpressionsMenu.Control.Parameter() {name = p?.name})
                    .ToArray(),
                labels = c.labels.ToArray(),
                style = c.style,
                value = c.value,
            };
        }
    }
}