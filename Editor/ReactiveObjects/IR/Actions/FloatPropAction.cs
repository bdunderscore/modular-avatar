#nullable enable

using System;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class FloatPropAction : IAction
    {
        internal PropertyTarget Prop { get; }
        internal float Value { get; }
        public object TargetKey => Prop;

        internal FloatPropAction(PropertyTarget prop, float value)
        {
            Prop = prop;
            Value = value;
        }

        internal static EditorCurveBinding? GetCurveBinding(PropertyTarget prop, ObjectPathRemapper remapper)
        {
            var targetObject = prop.TargetObject;
            var gameObject = targetObject switch
            {
                GameObject go => go,
                Component component => component.gameObject,
                _ => null
            };
            return gameObject == null
                ? null
                : EditorCurveBinding.FloatCurve(
                    remapper.GetVirtualPathForObject(gameObject),
                    targetObject.GetType(),
                    prop.PropertyName);
        }

        internal void Emit(ObjectPathRemapper remapper, VirtualClip clip)
        {
            var binding = GetCurveBinding(Prop, remapper);
            if (binding.HasValue)
                clip.SetFloatCurve(binding.Value, AnimationCurve.Constant(0, 1, Value));
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return other is FloatPropAction action && Prop.Equals(action.Prop) && Value.Equals(action.Value);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            if (Prop.TargetObject == null) return StaticApplyResult.Retain;

            var serializedObject = new SerializedObject(Prop.TargetObject);
            var property = serializedObject.FindProperty(Prop.PropertyName);
            if (property == null) return StaticApplyResult.Retain;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = Value != 0;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = Value;
                    break;
                default:
                    return StaticApplyResult.Retain;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return StaticApplyResult.Applied;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is FloatPropAction other &&
                                                  Prop.Equals(other.Prop) && Value.Equals(other.Value));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Prop, Value);
        }
    }
}