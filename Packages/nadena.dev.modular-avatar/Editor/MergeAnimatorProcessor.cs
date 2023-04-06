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

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MergeAnimatorProcessor
    {
        private const string SAMPLE_PATH_PACKAGE =
            "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";

        private const string SAMPLE_PATH_LEGACY = "Assets/VRCSDK/Examples3/Animation/Controllers";

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

            foreach (var merge in toMerge)
            {
                BuildReport.ReportingObject(merge, () => ProcessMergeAnimators(avatarGameObject, context, merge));
            }

            descriptor.baseAnimationLayers = FinishSessions(descriptor.baseAnimationLayers);
            descriptor.specialAnimationLayers = FinishSessions(descriptor.specialAnimationLayers);
        }

        private void ProcessMergeAnimators(GameObject avatarGameObject, BuildContext context,
            ModularAvatarMergeAnimator merge)
        {
            if (merge.animators.Count == 0) return;

            Dictionary<VRCAvatarDescriptor.AnimLayerType, int> controllerLayersCount =
                new Dictionary<VRCAvatarDescriptor.AnimLayerType, int>();

            foreach (AnimLayerData layerData in merge.animators)
            {
                if (mergeSessions.ContainsKey(layerData.type))
                {
                    controllerLayersCount[layerData.type] = mergeSessions[layerData.type].getLayerCount();
                }
            }

            string basePath;
            if (merge.pathMode == MergeAnimatorPathMode.Relative)
            {
                var relativePath = RuntimeUtil.RelativePath(avatarGameObject, merge.gameObject);
                basePath = relativePath != "" ? relativePath + "/" : "";
            }
            else
            {
                basePath = "";
            }

            foreach (AnimLayerData layerData in merge.animators)
            {
                ProcessMergeAnimator(context, layerData, basePath, merge.matchAvatarWriteDefaults, controllerLayersCount);
            }

            if (merge.deleteAttachedAnimator)
            {
                var animator = merge.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
            }
        }

        private void ProcessMergeAnimator(BuildContext context, AnimLayerData layerData, 
            string basePath, bool _writeDefaults, Dictionary<VRCAvatarDescriptor.AnimLayerType, int> layersCount)
        {
            if (!mergeSessions.TryGetValue(layerData.type, out var session))
            {
                session = new AnimatorCombiner(context, layersCount, layerData.type);
                mergeSessions[layerData.type] = session;
                if (defaultControllers_.ContainsKey(layerData.type))
                {
                    session.AddController("", defaultControllers_[layerData.type], null, layersCount);
                }
            }

            bool? writeDefaults = _writeDefaults ? writeDefaults_[layerData.type] : null;
            mergeSessions[layerData.type]
                .AddController(basePath, (AnimatorController)layerData.animator, writeDefaults, layersCount);
        }

        private VRCAvatarDescriptor.CustomAnimLayer[] FinishSessions(
            VRCAvatarDescriptor.CustomAnimLayer[] layers
        )
        {
            layers = (VRCAvatarDescriptor.CustomAnimLayer[]) layers.Clone();

            for (int i = 0; i < layers.Length; i++)
            {
                if (mergeSessions.TryGetValue(layers[i].type, out var session))
                {
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
                    mergeSessions[layer.type] = new AnimatorCombiner(_context, new Dictionary<VRCAvatarDescriptor.AnimLayerType, int>(), layer.type);
                    mergeSessions[layer.type].AddController("", controller, null, new Dictionary<VRCAvatarDescriptor.AnimLayerType, int>());
                }
            }
        }

        private bool? ProbeWriteDefaults(AnimatorController controller)
        {
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