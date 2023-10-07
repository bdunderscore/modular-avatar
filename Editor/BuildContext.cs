using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BuildContext
    {
        internal readonly nadena.dev.ndmf.BuildContext PluginBuildContext;

        internal VRCAvatarDescriptor AvatarDescriptor => PluginBuildContext.AvatarDescriptor;
        internal GameObject AvatarRootObject => PluginBuildContext.AvatarRootObject;
        internal Transform AvatarRootTransform => PluginBuildContext.AvatarRootTransform;

        internal AnimationDatabase AnimationDatabase =>
            PluginBuildContext.Extension<AnimationServicesContext>().AnimationDatabase;

        internal PathMappings PathMappings =>
            PluginBuildContext.Extension<AnimationServicesContext>().PathMappings;

        internal UnityEngine.Object AssetContainer => PluginBuildContext.AssetContainer;

        private bool SaveImmediate = false;

        internal readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> ClonedMenus
            = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

        public static implicit operator BuildContext(ndmf.BuildContext ctx) =>
            ctx.Extension<ModularAvatarContext>().BuildContext;

        /// <summary>
        /// This dictionary overrides the _original contents_ of ModularAvatarMenuInstallers. Notably, this does not
        /// replace the source menu for the purposes of identifying any other MAMIs that might install to the same
        /// menu asset.
        /// </summary>
        internal readonly Dictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>> PostProcessControls
            = new Dictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>();

        public BuildContext(nadena.dev.ndmf.BuildContext PluginBuildContext)
        {
            this.PluginBuildContext = PluginBuildContext;
        }

        public BuildContext(VRCAvatarDescriptor avatarDescriptor)
            : this(new ndmf.BuildContext(avatarDescriptor, null))
        {
        }

        public BuildContext(GameObject avatarGameObject)
            : this(new ndmf.BuildContext(avatarGameObject, null))
        {
        }

        public void SaveAsset(Object obj)
        {
            if (!SaveImmediate || AssetDatabase.IsMainAsset(obj) || AssetDatabase.IsSubAsset(obj)) return;

            AssetDatabase.AddObjectToAsset(obj, AssetContainer);
        }

        public AnimatorController CreateAnimator(AnimatorController toClone = null)
        {
            AnimatorController controller;
            if (toClone != null)
            {
                controller = Object.Instantiate(toClone);
            }
            else
            {
                controller = new AnimatorController();
            }

            SaveAsset(controller);

            return controller;
        }

        public AnimatorController DeepCloneAnimator(RuntimeAnimatorController controller)
        {
            if (controller == null) return null;

            var merger = new AnimatorCombiner(this, controller.name + " (clone)");
            switch (controller)
            {
                case AnimatorController ac:
                    merger.AddController("", ac, null);
                    break;
                case AnimatorOverrideController oac:
                    merger.AddOverrideController("", oac, null);
                    break;
                default:
                    throw new Exception("Unknown RuntimeAnimatorContoller type " + controller.GetType());
            }

            return merger.Finish();
        }

        public AnimatorController ConvertAnimatorController(AnimatorOverrideController overrideController)
        {
            var merger = new AnimatorCombiner(this, overrideController.name + " (clone)");
            merger.AddOverrideController("", overrideController, null);
            return merger.Finish();
        }

        public VRCExpressionsMenu CloneMenu(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            if (ClonedMenus.TryGetValue(menu, out var newMenu)) return newMenu;
            newMenu = Object.Instantiate(menu);
            this.SaveAsset(newMenu);
            ClonedMenus[menu] = newMenu;

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