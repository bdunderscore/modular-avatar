#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BuildContext
    {
        internal readonly ndmf.BuildContext PluginBuildContext;

#if MA_VRCSDK3_AVATARS
        internal VRCAvatarDescriptor AvatarDescriptor => PluginBuildContext.AvatarDescriptor;
#endif
        internal GameObject AvatarRootObject => PluginBuildContext.AvatarRootObject;
        internal Transform AvatarRootTransform => PluginBuildContext.AvatarRootTransform;

        private bool SaveImmediate = false;

#if MA_VRCSDK3_AVATARS
        internal readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> ClonedMenus
            = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

        /// <summary>
        /// This dictionary overrides the _original contents_ of ModularAvatarMenuInstallers. Notably, this does not
        /// replace the source menu for the purposes of identifying any other MAMIs that might install to the same
        /// menu asset.
        /// </summary>
        internal readonly Dictionary<Object, Action<VRCExpressionsMenu.Control>> PostProcessControls = new();
#endif
        public static implicit operator BuildContext(ndmf.BuildContext ctx) =>
            ctx.Extension<ModularAvatarContext>().BuildContext;

        public BuildContext(ndmf.BuildContext PluginBuildContext)
        {
            this.PluginBuildContext = PluginBuildContext;
        }

#if MA_VRCSDK3_AVATARS
        public BuildContext(VRCAvatarDescriptor avatarDescriptor)
            : this(new ndmf.BuildContext(avatarDescriptor, null))
        {
        }
#endif

        public BuildContext(GameObject avatarGameObject)
            : this(new ndmf.BuildContext(avatarGameObject, null))
        {
        }

        public void SaveAsset(Object obj)
        {
            if (!SaveImmediate || AssetDatabase.IsMainAsset(obj) || AssetDatabase.IsSubAsset(obj)) return;

            PluginBuildContext.AssetSaver.SaveAsset(obj);
        }


#if MA_VRCSDK3_AVATARS
        public VRCExpressionsMenu CloneMenu(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            if (ClonedMenus.TryGetValue(menu, out var newMenu)) return newMenu;
            newMenu = Object.Instantiate(menu);
            this.SaveAsset(newMenu);
            ClonedMenus[menu] = newMenu;

            foreach (var control in newMenu.controls)
            {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != newMenu)
                {
                    control.subMenu = CloneMenu(control.subMenu);
                }
            }

            return newMenu;
        }
#endif
    }
}