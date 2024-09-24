/*
 * MIT License
 *
 * Copyright (c) 2022 bd_
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MergeAnimatorProcessor
    {
        private const string SAMPLE_PATH_PACKAGE =
            "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";

        private const string SAMPLE_PATH_LEGACY = "Assets/VRCSDK/Examples3/Animation/Controllers";

        private const string GUID_GESTURE_HANDSONLY_MASK = "b2b8bad9583e56a46a3e21795e96ad92";

        private BuildContext _context;

        private Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorController> defaultControllers_ =
            new Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorController>();

        private Dictionary<VRCAvatarDescriptor.AnimLayerType, bool?> writeDefaults_ =
            new Dictionary<VRCAvatarDescriptor.AnimLayerType, bool?>();

        Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorCombiner> mergeSessions =
            new Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorCombiner>();

        internal void OnPreprocessAvatar(GameObject avatarGameObject, BuildContext context)
        {
            _context = context;

            defaultControllers_.Clear();
            mergeSessions.Clear();

            var descriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>();

            if (descriptor.baseAnimationLayers != null) InitSessions(descriptor.baseAnimationLayers);
            if (descriptor.specialAnimationLayers != null) InitSessions(descriptor.specialAnimationLayers);

            var toMerge = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
            Dictionary<VRCAvatarDescriptor.AnimLayerType, List<ModularAvatarMergeAnimator>> byLayerType
                = new Dictionary<VRCAvatarDescriptor.AnimLayerType, List<ModularAvatarMergeAnimator>>();

            foreach (var merge in toMerge)
            {
                if (!byLayerType.TryGetValue(merge.layerType, out var components))
                {
                    components = new List<ModularAvatarMergeAnimator>();
                    byLayerType[merge.layerType] = components;
                }

                components.Add(merge);
            }

            foreach (var entry in byLayerType)
            {
                ProcessLayerType(context, entry.Key, entry.Value);
            }

            descriptor.baseAnimationLayers = FinishSessions(descriptor.baseAnimationLayers);
            descriptor.specialAnimationLayers = FinishSessions(descriptor.specialAnimationLayers);
            descriptor.customizeAnimationLayers = true;
        }

        private void ProcessLayerType(
            BuildContext context,
            VRCAvatarDescriptor.AnimLayerType layerType,
            List<ModularAvatarMergeAnimator> toMerge
        )
        {
            // Stable sort
            var sorted = toMerge.OrderBy(x => x.layerPriority)
                .ToList();
            var beforeOriginal = sorted.Where(x => x.layerPriority < 0)
                .ToList();
            var afterOriginal = sorted.Where(x => x.layerPriority >= 0)
                .ToList();

            var session = new AnimatorCombiner(context.PluginBuildContext, layerType.ToString() + " (merged)");
            mergeSessions[layerType] = session;
            mergeSessions[layerType].BlendableLayer = BlendableLayerFor(layerType);

            foreach (var component in beforeOriginal)
            {
                MergeSingle(context, session, component);
            }

            if (defaultControllers_.TryGetValue(layerType, out var defaultController) &&
                defaultController.layers.Length > 0)
            {
                session.AddController("", defaultController, null, forceFirstLayerWeight: true);
            }

            foreach (var component in afterOriginal)
            {
                MergeSingle(context, session, component);
            }
        }

        private void MergeSingle(BuildContext context, AnimatorCombiner session, ModularAvatarMergeAnimator merge)
        {
            if (merge.animator == null)
            {
                return;
            }
            
            string basePath;
            if (merge.pathMode == MergeAnimatorPathMode.Relative)
            {
                var targetObject = merge.relativePathRoot.Get(context.AvatarRootTransform);
                if (targetObject == null) targetObject = merge.gameObject;

                var relativePath = RuntimeUtil.RelativePath(context.AvatarRootObject, targetObject);
                basePath = relativePath != "" ? relativePath + "/" : "";
            }
            else
            {
                basePath = "";
            }

            var writeDefaults = merge.matchAvatarWriteDefaults
                ? writeDefaults_.GetValueOrDefault(merge.layerType)
                : null;
            var controller = _context.ConvertAnimatorController(merge.animator);
            session.AddController(basePath, controller, writeDefaults);

            if (merge.deleteAttachedAnimator)
            {
                var animator = merge.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
            }
        }

        private VRCAvatarDescriptor.CustomAnimLayer[] FinishSessions(
            VRCAvatarDescriptor.CustomAnimLayer[] layers
        )
        {
            layers = (VRCAvatarDescriptor.CustomAnimLayer[])layers.Clone();

            // Ensure types are consistent across layers
            Dictionary<string, AnimatorControllerParameterType> types =
                new Dictionary<string, AnimatorControllerParameterType>();
            // Learn types...
            foreach (var session in mergeSessions.Values)
            {
                session.MergeTypes(types);
            }
            // And propagate them
            foreach (var session in mergeSessions.Values)
            {
                session.MergeTypes(types);
            }
            
            for (int i = 0; i < layers.Length; i++)
            {
                if (mergeSessions.TryGetValue(layers[i].type, out var session))
                {
                    if (layers[i].type == VRCAvatarDescriptor.AnimLayerType.Gesture && layers[i].isDefault)
                    {
                        // We need to set the mask field for the gesture layer on initial configuration
                        layers[i].mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                            AssetDatabase.GUIDToAssetPath(GUID_GESTURE_HANDSONLY_MASK)
                        );
                    }

                    layers[i].isDefault = false;
                    layers[i].animatorController = session.Finish();
                }
            }

            return layers;
        }

        private void InitSessions(VRCAvatarDescriptor.CustomAnimLayer[] layers)
        {
            foreach (var layer in layers)
            {
                var controller = ResolveLayerController(layer);
                if (controller == null) controller = new AnimatorController();

                defaultControllers_[layer.type] = controller;
                writeDefaults_[layer.type] = ProbeWriteDefaults(controller);
                if (!layer.isDefault)
                {
                    // For non-default layers, ensure we always clone the controller for the benefit of subsequent
                    // processing phases
                    mergeSessions[layer.type] =
                        new AnimatorCombiner(_context.PluginBuildContext, layer.type.ToString());
                    mergeSessions[layer.type].BlendableLayer = BlendableLayerFor(layer.type);
                    mergeSessions[layer.type].AddController("", controller, null);
                }
            }
        }

        private VRC_AnimatorLayerControl.BlendableLayer? BlendableLayerFor(VRCAvatarDescriptor.AnimLayerType layerType)
        {
            if (Enum.TryParse(layerType.ToString(), out VRC_AnimatorLayerControl.BlendableLayer result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        internal static bool? ProbeWriteDefaults(AnimatorController controller)
        {
            if (controller == null) return null;
            
            bool hasWDOn = false;
            bool hasWDOff = false;

            var stateMachineQueue = new Queue<AnimatorStateMachine>();
            foreach (var layer in controller.layers)
            {
                stateMachineQueue.Enqueue(layer.stateMachine);
            }

            while (stateMachineQueue.Count > 0)
            {
                var stateMachine = stateMachineQueue.Dequeue();
                foreach (var state in stateMachine.states)
                {
                    if (state.state.writeDefaultValues) hasWDOn = true;
                    else hasWDOff = true;
                }

                foreach (var child in stateMachine.stateMachines)
                {
                    stateMachineQueue.Enqueue(child.stateMachine);
                }
            }

            if (hasWDOn == hasWDOff) return null;
            return hasWDOn;
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
    }
}

#endif