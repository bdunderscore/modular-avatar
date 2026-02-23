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
            // TODO - handle non-float values
            if (_value is float f)
            {
                var binding = GetCurveBinding(context);
                if (binding == null) return;

                motion.SetFloatCurve(binding.Value, AnimationCurve.Constant(0, 1, f));
            }
        }

        private EditorCurveBinding? GetCurveBinding(BakeContext context)
        {
            var obj = _prop.TargetObject;

            GameObject gameObject;
            if (obj is GameObject go) gameObject = go;
            else if (obj is Component c) gameObject = c.gameObject;
            else return null;

            return EditorCurveBinding.FloatCurve(
                context.ObjectPathRemapper.GetVirtualPathForObject(gameObject),
                _prop.TargetObject.GetType(),
                _prop.PropertyName
            );
        }


        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            var targetObject = _prop.TargetObject;

            var binding = GetCurveBinding(context);
            if (!binding.HasValue) return;

            float? originalValue = null;

            if (targetObject is SkinnedMeshRenderer smr && _prop.PropertyName.StartsWith("blendShape."))
            {
                var shapeName = _prop.PropertyName[11..];
                var value = (float)_value;
                var index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                originalValue = smr.GetBlendShapeWeight(index);

                if (actionStartsActive)
                {
                    smr.SetBlendShapeWeight(index, value);
                }
            }
            else
            {
                var so = new SerializedObject(_prop.TargetObject);
                var staticState = (float)_value;

                // TODO - staticStateOverrides
                var prop = so.FindProperty(_prop.PropertyName);
                if (prop != null)
                {
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Boolean:
                            originalValue = prop.boolValue ? 1.0f : 0.0f;
                            prop.boolValue = staticState > 0.5f;
                            break;
                        case SerializedPropertyType.Float:
                            originalValue = prop.floatValue;
                            prop.floatValue = staticState;
                            break;
                        default:
                            // TODO - objectReference
                            return;
                    }
                }
            }

            if (originalValue.HasValue)
            {
                // We always set to the original active state to give other layers a chance to override
                context.BaseLayerClip.SetFloatCurve(
                    binding.Value,
                    AnimationCurve.Constant(0, 1, originalValue.Value)
                );
            }
        }
    }
}