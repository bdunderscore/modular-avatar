#region

using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

#endregion

namespace nadena.dev.modular_avatar.animation
{
    internal static class AnimationUtil
    {
        private const string SAMPLE_PATH_PACKAGE =
            "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";

        private const string SAMPLE_PATH_LEGACY = "Assets/VRCSDK/Examples3/Animation/Controllers";

        private const string GUID_GESTURE_HANDSONLY_MASK = "b2b8bad9583e56a46a3e21795e96ad92";


        public static AnimatorController DeepCloneAnimator(BuildContext context, RuntimeAnimatorController controller)
        {
            if (controller == null) return null;

            var merger = new AnimatorCombiner(context, controller.name + " (cloned)");
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

            var clone = merger.Finish();
            ObjectRegistry.RegisterReplacedObject(controller, clone);
            return clone;
        }

        internal static void CloneAllControllers(BuildContext context)
        {
            // Ensure all of the controllers on the avatar descriptor point to temporary assets.
            // This helps reduce the risk that we'll accidentally modify the original assets.

#if MA_VRCSDK3_AVATARS
            context.AvatarDescriptor.baseAnimationLayers =
                CloneLayers(context, context.AvatarDescriptor.baseAnimationLayers);
            context.AvatarDescriptor.specialAnimationLayers =
                CloneLayers(context, context.AvatarDescriptor.specialAnimationLayers);
#endif
        }

#if MA_VRCSDK3_AVATARS
        private static VRCAvatarDescriptor.CustomAnimLayer[] CloneLayers(
            BuildContext context,
            VRCAvatarDescriptor.CustomAnimLayer[] layers
        )
        {
            if (layers == null) return null;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.animatorController != null && !context.IsTemporaryAsset(layer.animatorController))
                {
                    layer.animatorController = DeepCloneAnimator(context, layer.animatorController);
                }

                layers[i] = layer;
            }

            return layers;
        }

        public static AnimatorController GetOrInitializeController(
            this BuildContext context,
            VRCAvatarDescriptor.AnimLayerType type)
        {
            return FindLayer(context.AvatarDescriptor.baseAnimationLayers)
                   ?? FindLayer(context.AvatarDescriptor.specialAnimationLayers);

            AnimatorController FindLayer(VRCAvatarDescriptor.CustomAnimLayer[] layers)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    if (layer.type == type)
                    {
                        if (layer.animatorController == null || layer.isDefault)
                        {
                            layer.animatorController = ResolveLayerController(layer);
                            if (type == VRCAvatarDescriptor.AnimLayerType.Gesture)
                            {
                                layer.mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                                    AssetDatabase.GUIDToAssetPath(GUID_GESTURE_HANDSONLY_MASK)
                                );
                            }

                            layers[i] = layer;
                        }

                        return layer.animatorController as AnimatorController;
                    }
                }

                return null;
            }
        }


        private static AnimatorController ResolveLayerController(VRCAvatarDescriptor.CustomAnimLayer layer)
        {
            AnimatorController controller = null;

            if (!layer.isDefault && layer.animatorController != null &&
                layer.animatorController is AnimatorController c)
            {
                controller = c;
            }
            else
            {
                string name;
                switch (layer.type)
                {
                    case VRCAvatarDescriptor.AnimLayerType.Action:
                        name = "Action";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Additive:
                        name = "Idle";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Base:
                        name = "Locomotion";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Gesture:
                        name = "Hands";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Sitting:
                        name = "Sitting";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.FX:
                        name = "Face";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.TPose:
                        name = "UtilityTPose";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.IKPose:
                        name = "UtilityIKPose";
                        break;
                    default:
                        name = null;
                        break;
                }

                if (name != null)
                {
                    name = "/vrc_AvatarV3" + name + "Layer.controller";

                    controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SAMPLE_PATH_PACKAGE + name);
                    if (controller == null)
                    {
                        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SAMPLE_PATH_LEGACY + name);
                    }
                }
            }

            return controller;
        }
#endif

        public static bool IsProxyAnimation(this Motion m)
        {
            var path = AssetDatabase.GetAssetPath(m);

            // This is a fairly wide condition in order to deal with:
            // 1. Future additions of proxy animations (so GUIDs are out)
            // 2. Unitypackage based installations of the VRCSDK
            // 3. VCC based installations of the VRCSDK
            // 4. Very old VCC based installations of the VRCSDK where proxy animations were copied into Assets
            return path.Contains("/AV3 Demo Assets/Animation/ProxyAnim/proxy")
                   || path.Contains("/VRCSDK/Examples3/Animation/ProxyAnim/proxy")
                   || path.StartsWith("Packages/com.vrchat.");
        }

        
        /// <summary>
        /// Enumerates all state machines and sub-state machines starting from a specific starting ASM
        /// </summary>
        /// <param name="ac"></param>
        /// <returns></returns>
        internal static IEnumerable<AnimatorStateMachine> ReachableStateMachines(this AnimatorStateMachine asm)
        {
            HashSet<AnimatorStateMachine> visitedStateMachines = new HashSet<AnimatorStateMachine>();
            Queue<AnimatorStateMachine> pending = new Queue<AnimatorStateMachine>();
            
            pending.Enqueue(asm);

            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (visitedStateMachines.Contains(next)) continue;
                visitedStateMachines.Add(next);

                foreach (var child in next.stateMachines)
                {
                    if (child.stateMachine != null) pending.Enqueue(child.stateMachine);
                }

                yield return next;
            }
        }
    }
}