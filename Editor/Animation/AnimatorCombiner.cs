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

#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.util;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

#endregion

namespace nadena.dev.modular_avatar.animation
{
    internal class AnimatorCombiner
    {
        private readonly BuildContext _context;
        private readonly AnimatorController _combined;

        private readonly DeepClone _deepClone;

        private List<AnimatorControllerLayer> _layers = new List<AnimatorControllerLayer>();

        private Dictionary<String, AnimatorControllerParameter> _parameters =
            new Dictionary<string, AnimatorControllerParameter>();

        private Dictionary<String, AnimatorController> _parameterSource =
            new Dictionary<string, AnimatorController>();

        private Dictionary<KeyValuePair<String, Motion>, Motion> _motions =
            new Dictionary<KeyValuePair<string, Motion>, Motion>();

        private Dictionary<KeyValuePair<String, AnimatorStateMachine>, AnimatorStateMachine> _stateMachines =
            new Dictionary<KeyValuePair<string, AnimatorStateMachine>, AnimatorStateMachine>();

        private Dictionary<Object, Object> _cloneMap;

        private int _controllerBaseLayer = 0;

#if MA_VRCSDK3_AVATARS
        public VRC_AnimatorLayerControl.BlendableLayer? BlendableLayer;
#endif

        public AnimatorCombiner(BuildContext context, String assetName)
        {
            _combined = new AnimatorController();
            if (context.AssetContainer != null && EditorUtility.IsPersistent(context.AssetContainer))
            {
                AssetDatabase.AddObjectToAsset(_combined, context.AssetContainer);
            }

            _combined.name = assetName;

            _context = context;
            _deepClone = new DeepClone(context);
        }

        public AnimatorController Finish()
        {
            FixTransitionTypeConflicts();
            PruneEmptyLayers();

            _combined.parameters = _parameters.Values.ToArray();
            _combined.layers = _layers.ToArray();
            return _combined;
        }

        public void MergeTypes(Dictionary<string, AnimatorControllerParameterType> types)
        {
            foreach (var p in _parameters.ToList())
            {
                if (types.TryGetValue(p.Key, out var outerValue))
                {
                    if (outerValue == p.Value.type) continue;
                    
                    if (outerValue == AnimatorControllerParameterType.Trigger
                        || p.Value.type == AnimatorControllerParameterType.Trigger)
                    {
                        BuildReport.LogFatal("error.merge_animator.param_type_mismatch",
                            p.Key,
                            p.Value.type,
                            outerValue
                        );
                    }

                    _parameters[p.Key].type = AnimatorControllerParameterType.Float;
                    types[p.Key] = AnimatorControllerParameterType.Float;
                }
                else
                {
                    types.Add(p.Key, p.Value.type);
                }
            }
        }

        /// <summary>
        /// When we merge multiple controllers with different types for the same parameter, we merge
        /// them all into using floats; thanks to VRChat's implicit typecasting, we can do this even for
        /// parameters registered as being ints or bools in the expressions parameter asset. However,
        /// we do need to fix any transitions to use the right transition types after this conversion.
        /// </summary>
        private void FixTransitionTypeConflicts()
        {
            foreach (var layer in _layers)
            {
                foreach (var asm in layer.stateMachine.ReachableStateMachines())
                {
                    foreach (ChildAnimatorState s in asm.states)
                    {
                        s.state.transitions = s.state.transitions.SelectMany(FixupTransition).ToArray();
                    }
                    
                    asm.entryTransitions = asm.entryTransitions
                        .SelectMany(FixupTransition).ToArray();
                    asm.anyStateTransitions = asm.anyStateTransitions
                        .SelectMany(FixupTransition).ToArray();

                    foreach (var stateMachine in asm.stateMachines)
                    {
                        var ssm = stateMachine.stateMachine;

                        var stateMachineTransitions = asm.GetStateMachineTransitions(ssm);
                        if (stateMachineTransitions.Length > 0)
                        {
                            asm.SetStateMachineTransitions(ssm,
                                stateMachineTransitions.SelectMany(FixupTransition).ToArray());
                        }
                    }
                }
            }
        }

        private IEnumerable<T> FixupTransition<T>(T t) where T: AnimatorTransitionBase, new()
        {
            if (!NeedsFixing(t.conditions))
            {
                yield return t;
                yield break;
            }
            
            AnimatorCondition[][][] combinations = t.conditions.Select(c => FixupCondition(c).ToArray()).ToArray();
            
            // Generate the combinatorial explosion of conditions needed to emulate NotEquals with floats...
            var conditions = ExplodeConditions(combinations).ToArray();

            if (conditions.Length == 1)
            {
                t.conditions = conditions[0];
                yield return t;
            }
            else
            {
                foreach (var conditionGroup in conditions)
                {
                    t.conditions = conditionGroup;
                    yield return t;

                    var newTransition = new T();
                    EditorUtility.CopySerialized(t, newTransition);
                    if (_context.AssetContainer != null)
                    {
                        AssetDatabase.AddObjectToAsset(newTransition, _context.AssetContainer);
                    }
                    t = newTransition;
                }
            }
        }

        private bool NeedsFixing(AnimatorCondition[] conditions)
        {
            return conditions.Any(c =>
            {
                if (!_parameters.TryGetValue(c.parameter, out var param)) return false;

                switch (c.mode)
                {
                    case AnimatorConditionMode.If when param.type != AnimatorControllerParameterType.Bool:
                    case AnimatorConditionMode.IfNot when param.type != AnimatorControllerParameterType.Bool:
                    case AnimatorConditionMode.Equals when param.type != AnimatorControllerParameterType.Int:
                    case AnimatorConditionMode.NotEqual when param.type != AnimatorControllerParameterType.Int:
                        return true;
                    default:
                        return false;
                }
            });
        }

        private IEnumerable<AnimatorCondition[]> ExplodeConditions(AnimatorCondition[][][] conditions)
        {
            int[] indices = new int[conditions.Length];

            while (true)
            {
                yield return conditions.SelectMany((group, i_) => group[indices[i_]]).ToArray();
                
                // Increment the rightmost possible counter
                int i;
                for (i = indices.Length - 1; i >= 0; i--)
                {
                    if (indices[i] < conditions[i].Length - 1)
                    {
                        indices[i]++;
                        // Unity 2019.....
                        // System.Array.Fill(indices, 0, i + 1, indices.Length - i - 1);
                        for (int j = i + 1; j < indices.Length; j++)
                        {
                            indices[j] = 0;
                        }
                        break;
                    }
                }

                if (i < 0) break;
            }
        }

        private IEnumerable<AnimatorCondition[]> FixupCondition(AnimatorCondition c)
        {
            if (!_parameters.TryGetValue(c.parameter, out var paramDef))
            {
                // Parameter is undefined, don't touch this condition
                yield return new[] { c };
                yield break;
            }
            
            switch (c.mode)
            {
                case AnimatorConditionMode.If when paramDef.type == AnimatorControllerParameterType.Float:
                {
                    c.mode = AnimatorConditionMode.Greater;
                    c.threshold = 0.5f;
                    yield return new[] { c };
                    break;
                }
                case AnimatorConditionMode.IfNot when paramDef.type == AnimatorControllerParameterType.Float:
                {
                    c.mode = AnimatorConditionMode.Less;
                    c.threshold = 0.5f;
                    yield return new[] { c };
                    break;
                }
                case AnimatorConditionMode.Equals when paramDef.type == AnimatorControllerParameterType.Float:
                {
                    var c1 = c;
                    var c2 = c;
                    c1.mode = AnimatorConditionMode.Greater;
                    c1.threshold -= 0.1f;
                    c2.mode = AnimatorConditionMode.Less;
                    c2.threshold += 0.1f;
                    yield return new[] { c1, c2 };
                    break;
                }
                case AnimatorConditionMode.NotEqual when paramDef.type == AnimatorControllerParameterType.Float:
                {
                    var origThresh = c.threshold;
                    c.mode = AnimatorConditionMode.Greater;
                    c.threshold = origThresh + 0.1f;
                    yield return new[] { c };

                    c.mode = AnimatorConditionMode.Less;
                    c.threshold = origThresh - 0.1f;
                    yield return new[] { c };
                    break;
                }
                default:
                    yield return new[] { c };
                    break;
            }
        }

        private void PruneEmptyLayers()
        {
#if MA_VRCSDK3_AVATARS
            // We can't safely correct the layer index of a VRCAnimatorLayerControl without knowing if it refers to
            // _this_ animator controller, so just skip this. We'll do the empty layer pruning later when we merge
            // everything together.
            if (BlendableLayer == null) return;
#endif
            
            var originalLayers = _layers;
            int[] layerIndexMappings = new int[originalLayers.Count];

            List<AnimatorControllerLayer> newLayers = new List<AnimatorControllerLayer>();

            for (int i = 0; i < originalLayers.Count; i++)
            {
                if (i > 0 && IsEmptyLayer(originalLayers[i]))
                {
                    layerIndexMappings[i] = -1;
                }
                else
                {
                    layerIndexMappings[i] = newLayers.Count;
                    newLayers.Add(originalLayers[i]);
                }
            }

            foreach (var layer in newLayers)
            {
                if (layer.stateMachine == null) continue;

                foreach (var asset in layer.stateMachine.ReferencedAssets(includeScene: false))
                {
                    if (asset is AnimatorState alc)
                    {
                        alc.behaviours = AdjustStateBehaviors(alc.behaviours);
                    }
                    else if (asset is AnimatorStateMachine asm)
                    {
                        asm.behaviours = AdjustStateBehaviors(asm.behaviours);
                    }
                }
            }

            _layers = newLayers;

            StateMachineBehaviour[] AdjustStateBehaviors(StateMachineBehaviour[] behaviours)
            {
                if (behaviours.Length == 0) return behaviours;

                var newBehaviors = new List<StateMachineBehaviour>();
                foreach (var b in behaviours)
                {
                    switch (b)
                    {
#if MA_VRCSDK3_AVATARS
                        case VRCAnimatorLayerControl alc when alc.playable == BlendableLayer:
                            int newLayer = -1;
                            if (alc.layer >= 0 && alc.layer < layerIndexMappings.Length)
                            {
                                newLayer = layerIndexMappings[alc.layer];
                            }

                            if (newLayer != -1)
                            {
                                alc.layer = newLayer;
                                newBehaviors.Add(alc);
                            }

                            break;
#endif
                        default:
                            newBehaviors.Add(b);
                            break;
                    }
                }

                return newBehaviors.ToArray();
            }
        }

        private bool IsEmptyLayer(AnimatorControllerLayer layer)
        {
            if (layer.syncedLayerIndex >= 0) return false;
            if (layer.avatarMask != null) return false;

            return layer.stateMachine == null
                   || (layer.stateMachine.states.Length == 0 && layer.stateMachine.stateMachines.Length == 0);
        }

        public void AddController(string basePath, AnimatorController controller, bool? writeDefaults,
            bool forceFirstLayerWeight = false)
        {
            _controllerBaseLayer = _layers.Count;
            _cloneMap = new Dictionary<Object, Object>();

            foreach (var param in controller.parameters)
            {
                if (_parameters.TryGetValue(param.name, out var acp))
                {
                    if (acp.type == param.type) continue;
                    
                    if (acp.type != param.type && 
                        (acp.type == AnimatorControllerParameterType.Trigger ||
                         param.type == AnimatorControllerParameterType.Trigger))
                    {
                        BuildReport.LogFatal("error.merge_animator.param_type_mismatch",
                            param.name,
                            acp.type.ToString(),
                            param.type.ToString(),
                            controller,
                            _parameterSource[param.name]
                        );
                    }

                    acp.type = AnimatorControllerParameterType.Float;

                    continue;
                }

                var clonedParameter = new AnimatorControllerParameter()
                {
                    name = param.name,
                    type = param.type,
                    defaultBool = param.defaultBool,
                    defaultFloat = param.defaultFloat,
                    defaultInt = param.defaultInt
                };

                _parameters.Add(param.name, clonedParameter);
                _parameterSource.Add(param.name, controller);
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
                avatarMask = _deepClone.DoClone(layer.avatarMask, basePath, _cloneMap),
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
                        var newMotion = _deepClone.DoClone(overrideMotion, basePath, _cloneMap);
                        newLayer.SetOverrideMotion((AnimatorState)_cloneMap[state], newMotion);
                    }

                    var overrideBehaviors = (StateMachineBehaviour[])layer.GetOverrideBehaviours(state)?.Clone();
                    if (overrideBehaviors != null)
                    {
                        for (int i = 0; i < overrideBehaviors.Length; i++)
                        {
                            overrideBehaviors[i] = _deepClone.DoClone(overrideBehaviors[i]);
                            AdjustBehavior(overrideBehaviors[i], basePath);
                        }

                        newLayer.SetOverrideBehaviours((AnimatorState)_cloneMap[state], overrideBehaviors);
                    }
                }

                newLayer.syncedLayerIndex += _controllerBaseLayer;
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
                    AdjustBehavior(behavior, basePath);
                }
            }

            _stateMachines[cacheKey] = asm;
            return asm;
        }

        private void AdjustBehavior(StateMachineBehaviour behavior, string basePath)
        {
#if MA_VRCSDK3_AVATARS
            switch (behavior)
            {
                case VRCAnimatorLayerControl layerControl:
                {
                    // TODO - need to figure out how to handle cross-layer references. For now this will handle
                    // intra-animator cases.
                    layerControl.layer += _controllerBaseLayer;
                    break;
                }
#if MA_VRCSDK3_AVATARS_3_5_2_OR_NEWER
                case VRCAnimatorPlayAudio playAudio:
                {
                    if (!string.IsNullOrEmpty(playAudio.SourcePath) && !string.IsNullOrEmpty(basePath) && !playAudio.SourcePath.StartsWith(basePath))
                    {
                        playAudio.SourcePath = $"{basePath}{playAudio.SourcePath}";
                    }
                    break;
                }
#endif
            }
#endif
        }
    }
}