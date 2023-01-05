using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BuildContext
    {
        internal readonly VRCAvatarDescriptor AvatarDescriptor;
        internal readonly AnimationDatabase AnimationDatabase = new AnimationDatabase();
        internal readonly AnimatorController AssetContainer;

        public BuildContext(VRCAvatarDescriptor avatarDescriptor)
        {
            AvatarDescriptor = avatarDescriptor;

            // AssetDatabase.CreateAsset is super slow - so only do it once, and add everything else as sub-assets.
            // This animator controller exists for the sole purpose of providing a placeholder to dump everything we
            // generate into.
            AssetContainer = new AnimatorController();
            AssetDatabase.CreateAsset(AssetContainer, Util.GenerateAssetPath());
        }

        public void SaveAsset(Object obj)
        {
            if (AssetDatabase.IsMainAsset(obj) || AssetDatabase.IsSubAsset(obj)) return;

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
            var merger = new AnimatorCombiner(this);
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
            var merger = new AnimatorCombiner(this);
            merger.AddOverrideController("", overrideController, null);
            return merger.Finish();
        }
    }
}