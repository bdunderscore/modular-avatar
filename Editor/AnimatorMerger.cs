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
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class AnimatorCombiner
    {
        private readonly AnimatorController _combined;
        private bool isSaved;

        private DeepClone _deepClone;
        
        private AnimatorOverrideController _overrideController;

        private List<AnimatorControllerLayer> _layers = new List<AnimatorControllerLayer>();

        private Dictionary<String, AnimatorControllerParameter> _parameters =
            new Dictionary<string, AnimatorControllerParameter>();

        private Dictionary<KeyValuePair<String, Motion>, Motion> _motions =
            new Dictionary<KeyValuePair<string, Motion>, Motion>();

        private Dictionary<KeyValuePair<String, AnimatorStateMachine>, AnimatorStateMachine> _stateMachines =
            new Dictionary<KeyValuePair<string, AnimatorStateMachine>, AnimatorStateMachine>();

        private Dictionary<Object, Object> _cloneMap;

        private int controllerBaseLayer = 0;

        public AnimatorCombiner(BuildContext context, String assetName)
        {
            _combined = context.CreateAnimator();
            isSaved = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_combined));
            _combined.name = assetName;

            _deepClone = new DeepClone(context.PluginBuildContext);
        }

        public AnimatorController Finish()
        {
            _combined.parameters = _parameters.Values.ToArray();
            _combined.layers = _layers.ToArray();
            return _combined;
        }

        public void AddController(string basePath, AnimatorController controller, bool? writeDefaults, bool forceFirstLayerWeight = false)
        {
            controllerBaseLayer = _layers.Count;
            _cloneMap = new Dictionary<Object, Object>();

            foreach (var param in controller.parameters)
            {
                if (_parameters.TryGetValue(param.name, out var acp))
                {
                    if (acp.type != param.type)
                    {
                        BuildReport.LogFatal("error.merge_animator.param_type_mismatch", new[]
                        {
                            param.name, acp.type.ToString(),
                            param.type.ToString()
                        });
                    }

                    continue;
                }

                _parameters.Add(param.name, param);
            }

            bool first = true;
            var layers = controller.layers;
            foreach (var layer in layers)
            {
                insertLayer(basePath, layer, first, writeDefaults, layers);
                if (first && forceFirstLayerWeight)
                {
                    _layers[_layers.Count - 1].defaultWeight = 1;
                }
                
                first = false;
            }
        }

        public void AddOverrideController(string basePath, AnimatorOverrideController overrideController,
            bool? writeDefaults)
        {
            AnimatorController controller = overrideController.runtimeAnimatorController as AnimatorController;
            if (controller == null) return;
            _deepClone.OverrideController = overrideController;
            try
            {
                this.AddController(basePath, controller, writeDefaults);
            }
            finally
            {
                _overrideController = null;
            }
        }

        private void insertLayer(
            string basePath,
            AnimatorControllerLayer layer,
            bool first,
            bool? writeDefaults,
            AnimatorControllerLayer[] layers
        )
        {
            var newLayer = new AnimatorControllerLayer()
            {
                name = layer.name,
                avatarMask = layer.avatarMask, // TODO map transforms
                blendingMode = layer.blendingMode,
                defaultWeight = first ? 1 : layer.defaultWeight,
                syncedLayerIndex = layer.syncedLayerIndex,
                syncedLayerAffectsTiming = layer.syncedLayerAffectsTiming,
                iKPass = layer.iKPass,
                stateMachine = mapStateMachine(basePath, layer.stateMachine),
            };

            UpdateWriteDefaults(newLayer.stateMachine, writeDefaults);

            if (newLayer.syncedLayerIndex != -1 && newLayer.syncedLayerIndex >= 0 &&
                newLayer.syncedLayerIndex < layers.Length)
            {
                // Transfer any motion overrides onto the new synced layer
                var baseLayer = layers[newLayer.syncedLayerIndex];
                foreach (var state in WalkAllStates(baseLayer.stateMachine))
                {
                    var overrideMotion = layer.GetOverrideMotion(state);
                    if (overrideMotion != null)
                    {
                        newLayer.SetOverrideMotion((AnimatorState) _cloneMap[state], overrideMotion);
                    }

                    var overrideBehaviors = (StateMachineBehaviour[]) layer.GetOverrideBehaviours(state)?.Clone();
                    if (overrideBehaviors != null)
                    {
                        for (int i = 0; i < overrideBehaviors.Length; i++)
                        {
                            overrideBehaviors[i] = _deepClone.DoClone(overrideBehaviors[i]);
                            AdjustBehavior(overrideBehaviors[i]);
                        }

                        newLayer.SetOverrideBehaviours((AnimatorState) _cloneMap[state], overrideBehaviors);
                    }
                }

                newLayer.syncedLayerIndex += controllerBaseLayer;
            }

            _layers.Add(newLayer);
        }

        IEnumerable<AnimatorState> WalkAllStates(AnimatorStateMachine animatorStateMachine)
        {
            HashSet<Object> visited = new HashSet<Object>();

            foreach (var state in VisitStateMachine(animatorStateMachine))
            {
                yield return state;
            }

            IEnumerable<AnimatorState> VisitStateMachine(AnimatorStateMachine layerStateMachine)
            {
                if (!visited.Add(layerStateMachine)) yield break;

                foreach (var state in layerStateMachine.states)
                {
                    if (state.state == null) continue;

                    yield return state.state;
                }

                foreach (var child in layerStateMachine.stateMachines)
                {
                    if (child.stateMachine == null) continue;

                    if (visited.Contains(child.stateMachine)) continue;
                    visited.Add(child.stateMachine);
                    foreach (var state in VisitStateMachine(child.stateMachine))
                    {
                        yield return state;
                    }
                }
            }
        }

        private void UpdateWriteDefaults(AnimatorStateMachine stateMachine, bool? writeDefaults)
        {
            if (!writeDefaults.HasValue) return;

            var queue = new Queue<AnimatorStateMachine>();
            queue.Enqueue(stateMachine);
            while (queue.Count > 0)
            {
                var sm = queue.Dequeue();
                foreach (var state in sm.states)
                {
                    state.state.writeDefaultValues = writeDefaults.Value;
                }

                foreach (var child in sm.stateMachines)
                {
                    queue.Enqueue(child.stateMachine);
                }
            }
        }

        private AnimatorStateMachine mapStateMachine(string basePath, AnimatorStateMachine layerStateMachine)
        {
            var cacheKey = new KeyValuePair<string, AnimatorStateMachine>(basePath, layerStateMachine);

            if (_stateMachines.TryGetValue(cacheKey, out var asm))
            {
                return asm;
            }

            asm = _deepClone.DoClone(layerStateMachine, basePath, _cloneMap);

            foreach (var state in WalkAllStates(asm))
            {
                foreach (var behavior in state.behaviours)
                {
                    AdjustBehavior(behavior);
                }
            }

            _stateMachines[cacheKey] = asm;
            return asm;
        }

        private void AdjustBehavior(StateMachineBehaviour behavior)
        {
#if MA_VRCSDK3_AVATARS
            switch (behavior)
            {
                case VRCAnimatorLayerControl layerControl:
                {
                    // TODO - need to figure out how to handle cross-layer references. For now this will handle
                    // intra-animator cases.
                    layerControl.layer += controllerBaseLayer;
                    break;
                }
            }
#endif
        }
    }
}