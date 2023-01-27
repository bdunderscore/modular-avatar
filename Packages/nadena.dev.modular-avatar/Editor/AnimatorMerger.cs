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
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class AnimatorCombiner
    {
        private readonly AnimatorController _combined;

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

        public AnimatorCombiner(BuildContext context)
        {
            _combined = context.CreateAnimator();
        }

        public AnimatorController Finish()
        {
            _combined.parameters = _parameters.Values.ToArray();
            _combined.layers = _layers.ToArray();
            return _combined;
        }

        public void AddController(string basePath, AnimatorController controller, bool? writeDefaults)
        {
            controllerBaseLayer = _layers.Count;
            _cloneMap = new Dictionary<Object, Object>();

            foreach (var param in controller.parameters)
            {
                if (_parameters.TryGetValue(param.name, out var acp))
                {
                    if (acp.type != param.type)
                    {
                        throw new Exception(
                            $"Parameter {param.name} has different types in {basePath} and {controller.name}");
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
                first = false;
            }
        }

        public void AddOverrideController(string basePath, AnimatorOverrideController overrideController,
            bool? writeDefaults)
        {
            AnimatorController controller = overrideController.runtimeAnimatorController as AnimatorController;
            if (controller == null) return;
            _overrideController = overrideController;
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
                            overrideBehaviors[i] = deepClone(overrideBehaviors[i], x => x,
                                new Dictionary<Object, Object>());
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

            asm = deepClone(layerStateMachine, (obj) => customClone(obj, basePath), _cloneMap);

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
        }

        private static string MapPath(EditorCurveBinding binding, string basePath)
        {
            if (binding.type == typeof(Animator) && binding.path == "")
            {
                return "";
            }
            else
            {
                var newPath = binding.path == "" ? basePath : basePath + binding.path;
                if (newPath.EndsWith("/"))
                {
                    newPath = newPath.Substring(0, newPath.Length - 1);
                }

                return newPath;
            }
        }

        private Object customClone(Object o, string basePath)
        {
            if (o is AnimationClip clip)
            {
                if (basePath == "" || Util.IsProxyAnimation(clip)) return clip;

                AnimationClip newClip = new AnimationClip();
                newClip.name = "rebased " + clip.name;
                AssetDatabase.AddObjectToAsset(newClip, _combined);

                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var newBinding = binding;
                    newBinding.path = MapPath(binding, basePath);
                    newClip.SetCurve(newBinding.path, newBinding.type, newBinding.propertyName,
                        AnimationUtility.GetEditorCurve(clip, binding));
                }

                foreach (var objBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var newBinding = objBinding;
                    newBinding.path = MapPath(objBinding, basePath);
                    AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                        AnimationUtility.GetObjectReferenceCurve(clip, objBinding));
                }

                newClip.wrapMode = clip.wrapMode;
                newClip.legacy = clip.legacy;
                newClip.frameRate = clip.frameRate;
                newClip.localBounds = clip.localBounds;
                AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(clip));

                return newClip;
            }
            else if (o is Texture)
            {
                return o;
            }
            else
            {
                return null;
            }
        }

        private T deepClone<T>(T original,
            Func<Object, Object> visitor,
            Dictionary<Object, Object> cloneMap
        ) where T : Object
        {
            if (original == null) return null;

            // We want to avoid trying to copy assets not part of the animation system (eg - textures, meshes,
            // MonoScripts...), so check for the types we care about here
            switch (original)
            {
                // Any object referenced by an animator that we intend to mutate needs to be listed here.
                case Motion _:
                case AnimatorController _:
                case AnimatorState _:
                case AnimatorStateMachine _:
                case AnimatorTransitionBase _:
                case StateMachineBehaviour _:
                    break; // We want to clone these types

                // Leave textures, materials, and script definitions alone
                case Texture2D _:
                case MonoScript _:
                case Material _:
                    return original;

                // Also avoid copying unknown scriptable objects.
                // This ensures compatibility with e.g. avatar remote, which stores state information in a state
                // behaviour referencing a custom ScriptableObject
                case ScriptableObject _:
                    return original;

                default:
                    throw new Exception($"Unknown type referenced from animator: {original.GetType()}");
            }

            // When using AnimatorOverrideController, replace the original AnimationClip based on AnimatorOverrideController.
            if (_overrideController != null && original is AnimationClip srcClip)
            {
                T overrideClip = _overrideController[srcClip] as T;
                if (overrideClip != null)
                {
                    original = overrideClip;
                }
            }

            if (cloneMap.ContainsKey(original))
            {
                return (T) cloneMap[original];
            }

            var obj = visitor(original);
            if (obj != null)
            {
                cloneMap[original] = obj;
                return (T) obj;
            }

            var ctor = original.GetType().GetConstructor(Type.EmptyTypes);
            if (ctor == null || original is ScriptableObject)
            {
                obj = Object.Instantiate(original);
            }
            else
            {
                obj = (T) ctor.Invoke(Array.Empty<object>());
                EditorUtility.CopySerialized(original, obj);
            }

            cloneMap[original] = obj;

            AssetDatabase.AddObjectToAsset(obj, _combined);

            SerializedObject so = new SerializedObject(obj);
            SerializedProperty prop = so.GetIterator();

            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = true;
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        prop.objectReferenceValue = deepClone(prop.objectReferenceValue, visitor, cloneMap);
                        break;
                    // Iterating strings can get super slow...
                    case SerializedPropertyType.String:
                        enterChildren = false;
                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            return (T) obj;
        }
    }
}