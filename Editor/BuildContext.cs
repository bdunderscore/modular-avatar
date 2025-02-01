#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif
using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
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

        internal AnimationDatabase AnimationDatabase =>
            PluginBuildContext.Extension<AnimationServicesContext>().AnimationDatabase;

        internal PathMappings PathMappings =>
            PluginBuildContext.Extension<AnimationServicesContext>().PathMappings;

        internal Object AssetContainer => PluginBuildContext.AssetContainer;

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

            var merger = new AnimatorCombiner(PluginBuildContext, controller.name + " (clone)");
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

            var result = merger.Finish();

            ObjectRegistry.RegisterReplacedObject(controller, result);

            return result;
        }

        public AnimatorController ConvertAnimatorController(RuntimeAnimatorController anyController)
        {
            switch (anyController)
            {
                case AnimatorController ac:
                    return ac;
                case AnimatorOverrideController aoc:
                    var merger = new AnimatorCombiner(PluginBuildContext, anyController.name + " (clone)");
                    merger.AddOverrideController("", aoc, null);
                    return merger.Finish();
                default:
                    throw new Exception("Unknown RuntimeAnimatorContoller type " + anyController.GetType());
            }
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