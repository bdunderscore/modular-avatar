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
using Object = UnityEngine.Object;

namespace net.fushizen.modular_avatar.core.editor
{
    public class AnimatorCombiner
    {
        private readonly AnimatorController _combined;

        private List<AnimatorControllerLayer> _layers = new List<AnimatorControllerLayer>();

        private Dictionary<String, AnimatorControllerParameter> _parameters =
            new Dictionary<string, AnimatorControllerParameter>();

        private Dictionary<KeyValuePair<String, Motion>, Motion> _motions =
            new Dictionary<KeyValuePair<string, Motion>, Motion>();

        private Dictionary<KeyValuePair<String, AnimatorStateMachine>, AnimatorStateMachine> _stateMachines =
            new Dictionary<KeyValuePair<string, AnimatorStateMachine>, AnimatorStateMachine>();

        public AnimatorCombiner()
        {
            _combined = Util.CreateAnimator();
        }

        public AnimatorController Finish()
        {
            _combined.parameters = _parameters.Values.ToArray();
            _combined.layers = _layers.ToArray();
            return _combined;
        }

        public void AddController(String basePath, AnimatorController controller)
        {
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
            foreach (var layer in controller.layers)
            {
                insertLayer(basePath, layer, first);
                first = false;
            }
        }

        private void insertLayer(string basePath, AnimatorControllerLayer layer, bool first)
        {
            var newLayer = new AnimatorControllerLayer()
            {
                name = layer.name,
                avatarMask = layer.avatarMask, // TODO map transforms
                blendingMode = layer.blendingMode,
                defaultWeight = first ? 1 : layer.defaultWeight,
                syncedLayerIndex = layer.syncedLayerIndex, // TODO
                syncedLayerAffectsTiming = layer.syncedLayerAffectsTiming, // TODO
                iKPass = layer.iKPass,
                stateMachine = mapStateMachine(basePath, layer.stateMachine),
            };

            _layers.Add(newLayer);
        }

        private AnimatorStateMachine mapStateMachine(string basePath, AnimatorStateMachine layerStateMachine)
        {
            var cacheKey = new KeyValuePair<string, AnimatorStateMachine>(basePath, layerStateMachine);

            if (_stateMachines.TryGetValue(cacheKey, out var asm))
            {
                return asm;
            }

            asm = deepClone(layerStateMachine, (obj) => customClone(obj, basePath));

            _stateMachines[cacheKey] = asm;
            return asm;
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

                return PathMappings.MapPath(newPath, binding.type == typeof(Transform));
            }
        }

        private Object customClone(Object o, string basePath)
        {
            if (basePath == "") return null;

            if (o is AnimationClip clip)
            {
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
            Dictionary<Object, Object> cloneMap = null
        ) where T : Object
        {
            if (original == null) return null;

            if (cloneMap == null) cloneMap = new Dictionary<Object, Object>();

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