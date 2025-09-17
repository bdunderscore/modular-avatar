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
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MergeAnimatorProcessor
    {
        private AnimatorServicesContext _asc;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            ModularAvatarMergeAnimator.GetMotionBasePathCallback = (merge, objectBuildContext) =>
            {
                if (merge.pathMode == MergeAnimatorPathMode.Absolute) return "";

                var context = (ndmf.BuildContext)objectBuildContext;

                var targetObject = merge.relativePathRoot.Get(context.AvatarRootTransform);
                if (targetObject == null) targetObject = merge.gameObject;

                var relativePath = RuntimeUtil.RelativePath(context.AvatarRootObject, targetObject);
                return relativePath != "" ? relativePath : "";
            };
        }
        
        internal void OnPreprocessAvatar(GameObject avatarGameObject, BuildContext context)
        {
            _asc = context.PluginBuildContext.Extension<AnimatorServicesContext>();
            
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
        }

        private void ProcessLayerType(
            BuildContext context,
            VRCAvatarDescriptor.AnimLayerType layerType,
            List<ModularAvatarMergeAnimator> toMerge
        )
        {
            // Layer priority sorting is handled by NDMF, so we just need to worry about replace mode going first
            var sorted = toMerge.OrderBy(x => x.mergeAnimatorMode == MergeAnimatorMode.Append)
                .ToList();

            var controller = _asc.ControllerContext.Controllers[layerType];

            var replacements = sorted.Count(x => x.mergeAnimatorMode == MergeAnimatorMode.Replace);
            if (replacements > 1)
            {
                BuildReport.LogFatal("error.merge_animator.multiple_replacements",
                    sorted.Where(x => x.mergeAnimatorMode == MergeAnimatorMode.Replace).ToArray<object>());
            }
            else if (replacements == 1)
            {
                // Delete all pre-existing layers.
                // Retain the blend tree layer, since that will generally be placed as the first layer in the animator
                controller.RemoveLayers(l => l.Name != MergeBlendTreePass.BlendTreeLayerName);

                // Merge just the first controller (the one that replaces)
                MergeSingle(context, controller, sorted.First(), null);
                sorted.RemoveAt(0);

                // We'll now continue processing the rest as normal.
            }

            var writeDefaults = AnalyzeLayerWriteDefaults(controller);

            foreach (var component in sorted)
            {
                MergeSingle(context, controller, component, writeDefaults);
            }
        }

        internal static bool? AnalyzeLayerWriteDefaults(VirtualAnimatorController controller)
        {
            bool? writeDefaults = null;

            var wdStateCounter = controller.Layers
                .Where(l => l.StateMachine != null)
                .Where(l => !IsWriteDefaultsRequiredLayer(l))
                .SelectMany(l => l.StateMachine!.AllStates())
                .Select(s => s.WriteDefaultValues)
                .GroupBy(b => b)
                .ToDictionary(g => g.Key, g => g.Count());

            if (wdStateCounter.Count == 1) writeDefaults = wdStateCounter.First().Key;
            return writeDefaults;
        }

        private void MergeSingle(BuildContext context, VirtualAnimatorController targetController,
            ModularAvatarMergeAnimator merge, bool? initialWriteDefaults)
        {
            if (merge.animator == null)
            {
                return;
            }

            if (!merge.matchAvatarWriteDefaults)
            {
                initialWriteDefaults = null;
            }
            
            var vac = context.PluginBuildContext.Extension<VirtualControllerContext>();

            if (!vac.Controllers.TryGetValue(merge, out var clonedController)) return;
            
            var firstLayer = clonedController.Layers.FirstOrDefault();
            // the first layer in an animator controller always has weight 1.0f (regardless of what is serialized)
            if (firstLayer != null) firstLayer.DefaultWeight = 1.0f;

            // Update parameters before we merge layers - this gives the VRChat platform bindings a chance to correct
            // any parameter drivers in the original (or destination) controller.
            var destParams = targetController.Parameters;
            var clonedParams = clonedController.Parameters;
            foreach (var (name, parameter) in clonedController.Parameters)
            {
                if (targetController.Parameters.TryGetValue(name, out var existingParam))
                {
                    if (existingParam.type != parameter.type)
                    {
                        // Force to float
                        existingParam = new AnimatorControllerParameter
                        {
                            type = AnimatorControllerParameterType.Float,
                            name = name,
                            defaultFloat = existingParam.type switch
                            {
                                AnimatorControllerParameterType.Bool => existingParam.defaultBool ? 1 : 0,
                                AnimatorControllerParameterType.Int => existingParam.defaultInt,
                                _ => 0
                            }
                        };

                        destParams = destParams.SetItem(name, existingParam);
                        clonedParams = clonedParams.SetItem(name, existingParam);
                    }

                    continue;
                }

                destParams = destParams.SetItem(name, parameter);
            }

            targetController.Parameters = destParams;
            clonedController.Parameters = clonedParams;

            // Parameter driver correction may have introduced new temporary parameters, so we need to re-check
            destParams = targetController.Parameters;
            foreach (var (name, parameter) in clonedController.Parameters)
            {
                if (!destParams.ContainsKey(name))
                {
                    destParams = destParams.SetItem(name, parameter);
                }
            }

            targetController.Parameters = destParams;
            
            foreach (var l in clonedController.Layers)
            {
                if (l.StateMachine == null)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal, "error.merge_animator.layer_no_state_machine",
                        l.Name, merge, merge.animator);
                    continue;
                }

                var isWriteDefaultsRequiredLayer = IsWriteDefaultsRequiredLayer(l);
                if (isWriteDefaultsRequiredLayer)
                {
                    // Force WD on for single state direct blendtree layers and additive layers
                    foreach (var state in l.StateMachine?.AllStates() ?? Array.Empty<VirtualState>())
                    {
                        state.WriteDefaultValues = true;
                    }
                }
                else if (initialWriteDefaults != null)
                {
                    foreach (var s in l.StateMachine?.AllStates() ?? Array.Empty<VirtualState>())
                    {
                        s.WriteDefaultValues = initialWriteDefaults.Value;
                    }
                }
                
                targetController.AddLayer(new LayerPriority(merge.layerPriority), l);
            }

            if (merge.deleteAttachedAnimator)
            {
                var animator = merge.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
            }

            Object.DestroyImmediate(merge);
        }

        private static bool IsWriteDefaultsRequiredLayer(VirtualLayer virtualLayer)
        {
            if (virtualLayer.BlendingMode == AnimatorLayerBlendingMode.Additive) return true;
            var sm = virtualLayer.StateMachine;
            if (sm == null) return false;

            if (sm.StateMachines.Count != 0) return false;
            if (sm.States.Count != 1) return false;
            if (sm.AnyStateTransitions.Count != 0) return false;
            if (sm.DefaultState?.Transitions?.Count != 0) return false; // also checks for nullity
            if (sm.DefaultState.Motion is not VirtualBlendTree) return false;

            return sm.DefaultState.Motion.AllReachableNodes()
                .OfType<VirtualBlendTree>()
                .Any(bt => bt.BlendType == BlendTreeType.Direct);
        }
    }
}

#endif