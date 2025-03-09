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

using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor;
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
            // Stable sort
            var sorted = toMerge.OrderBy(x => x.layerPriority)
                .ToList();
            var beforeOriginal = sorted.Where(x => x.layerPriority < 0)
                .ToList();
            var afterOriginal = sorted.Where(x => x.layerPriority >= 0)
                .ToList();

            var controller = _asc.ControllerContext[layerType];
            
            var wdStateCounter = controller.Layers.SelectMany(l => l.StateMachine.AllStates())
                .Select(s => s.WriteDefaultValues)
                .GroupBy(b => b)
                .ToDictionary(g => g.Key, g => g.Count());

            bool? writeDefaults = null;
            if (wdStateCounter.Count == 1) writeDefaults = wdStateCounter.First().Key;
            
            foreach (var component in sorted)
            {
                MergeSingle(context, controller, component, writeDefaults);
            }
        }

        private void MergeSingle(BuildContext context, VirtualAnimatorController targetController,
            ModularAvatarMergeAnimator merge, bool? initialWriteDefaults)
        {
            if (merge.animator == null)
            {
                return;
            }

            var vac = context.PluginBuildContext.Extension<VirtualControllerContext>();

            if (!vac.Controllers.TryGetValue(merge, out var clonedController)) return;
            
            var firstLayer = clonedController.Layers.FirstOrDefault();
            // the first layer in an animator controller always has weight 1.0f (regardless of what is serialized)
            if (firstLayer != null) firstLayer.DefaultWeight = 1.0f;

            foreach (var l in clonedController.Layers)
            {
                if (initialWriteDefaults != null)
                {
                    foreach (var s in l.StateMachine.AllStates())
                    {
                        s.WriteDefaultValues = initialWriteDefaults.Value;
                    }
                }

                targetController.AddLayer(new LayerPriority(merge.layerPriority), l);
            }

            foreach (var (name, parameter) in clonedController.Parameters)
            {
                if (targetController.Parameters.TryGetValue(name, out var existingParam))
                {
                    if (existingParam.type != parameter.type)
                    {
                        // Force to float
                        switch (parameter.type)
                        {
                            case AnimatorControllerParameterType.Bool:
                                existingParam.defaultFloat = existingParam.defaultBool ? 1.0f : 0.0f;
                                break;
                            case AnimatorControllerParameterType.Int:
                                existingParam.defaultFloat = existingParam.defaultInt;
                                break;
                        }

                        existingParam.type = AnimatorControllerParameterType.Float;

                        targetController.Parameters = targetController.Parameters.SetItem(name, existingParam);
                    }
                    continue;
                }

                targetController.Parameters = targetController.Parameters.Add(name, parameter);
            }
            
            if (merge.deleteAttachedAnimator)
            {
                var animator = merge.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
            }

            Object.DestroyImmediate(merge);
        }
    }
}

#endif