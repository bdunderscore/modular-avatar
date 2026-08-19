#nullable enable

using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class PropAction : IAction
    {
        public PropAction(TargetProp prop, object? value = null)
        {
            _prop = prop;
            _value = value;
        }

        private readonly TargetProp _prop;
        private readonly object? _value;
        public object TargetKey => _prop;

        public void ToMotion(BakeContext context, VirtualClip motion)
        {
            var binding = GetCurveBinding(context);
            if (binding == null) return;
            
            if (_value is float f)
            {
                motion.SetFloatCurve(binding.Value, AnimationCurve.Constant(0, 1, f));
            }
            else if (_value is Object obj)
            {
                motion.SetObjectCurve(binding.Value, new[]
                {
                    new ObjectReferenceKeyframe
                    {
                        time = 0,
                        value = obj
                    }
                });
            }
        }

        private EditorCurveBinding? GetCurveBinding(BakeContext context)
        {
            var obj = _prop.TargetObject;

            GameObject gameObject;
            if (obj is GameObject go) gameObject = go;
            else if (obj is Component c) gameObject = c.gameObject;
            else return null;

            if (_value is float)
            {
                return EditorCurveBinding.FloatCurve(
                    context.ObjectPathRemapper.GetVirtualPathForObject(gameObject),
                    _prop.TargetObject.GetType(),
                    _prop.PropertyName
                );
            }
            else
            {
                return EditorCurveBinding.PPtrCurve(
                    context.ObjectPathRemapper.GetVirtualPathForObject(gameObject),
                    _prop.TargetObject.GetType(),
                    _prop.PropertyName
                );
            }
        }


        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            var targetObject = _prop.TargetObject;

            var binding = GetCurveBinding(context);
            if (!binding.HasValue) return;

            object? originalValue = null;

            if (targetObject is SkinnedMeshRenderer smr && _prop.PropertyName.StartsWith("blendShape."))
            {
                var shapeName = _prop.PropertyName[11..];
                var index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                originalValue = smr.GetBlendShapeWeight(index);
            }
            else
            {
                var so = new SerializedObject(_prop.TargetObject);
                var prop = so.FindProperty(_prop.PropertyName);
                if (prop != null)
                {
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Boolean:
                            originalValue = prop.boolValue ? 1.0f : 0.0f;
                            break;
                        case SerializedPropertyType.Float:
                            originalValue = prop.floatValue;
                            break;
                        case SerializedPropertyType.ObjectReference:
                            originalValue = prop.objectReferenceValue;
                            break;
                        default:
                            return;
                    }
                }
            }

            // We always set to the original active state to give other layers a chance to override
            if (originalValue is float f)
            {
                context.BaseLayerClip.SetFloatCurve(
                    binding.Value,
                    AnimationCurve.Constant(0, 1, f)
                );
            }
            else if (originalValue is Object obj)
            {
                context.BaseLayerClip.SetObjectCurve(
                    binding.Value,
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0,
                            value = obj
                        }
                    }
                );
            }
        }
    }
}