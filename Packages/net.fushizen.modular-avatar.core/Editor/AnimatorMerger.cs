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
            _combined = Util.CreateContainer();
        }

        public AnimatorController Finish()
        {
            _combined.parameters = _parameters.Values.ToArray();
            _combined.layers = _layers.ToArray();
            AssetDatabase.SaveAssets();
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
                        throw new Exception($"Parameter {param.name} has different types in {basePath} and {controller.name}");
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
                    newBinding.path = PathMappings.MapPath(basePath + binding.path);
                    newClip.SetCurve(newBinding.path, newBinding.type, newBinding.propertyName,
                        AnimationUtility.GetEditorCurve(clip, binding));
                }
                
                foreach (var objBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var newBinding = objBinding;
                    newBinding.path = PathMappings.MapPath(basePath + objBinding.path);
                    AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                        AnimationUtility.GetObjectReferenceCurve(clip, objBinding));
                }

                newClip.wrapMode = clip.wrapMode;
                newClip.legacy = clip.legacy;
                newClip.frameRate = clip.frameRate;
                newClip.localBounds = clip.localBounds;
                AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(clip));

                return newClip;
            } else if (o is Texture)
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
            if (ctor == null || obj is ScriptableObject)
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