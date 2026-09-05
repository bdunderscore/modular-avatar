#nullable enable

using System;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class ObjectPropAction : IAction
    {
        internal PropertyTarget Prop { get; }
        internal Object? Value { get; }
        public object TargetKey => Prop;

        internal ObjectPropAction(PropertyTarget prop, Object? value = null)
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
                : EditorCurveBinding.PPtrCurve(
                    remapper.GetVirtualPathForObject(gameObject),
                    targetObject.GetType(),
                    prop.PropertyName);
        }

        internal void Emit(ObjectPathRemapper remapper, VirtualClip clip)
        {
            var binding = GetCurveBinding(Prop, remapper);
            if (binding.HasValue)
                clip.SetObjectCurve(binding.Value,
                    new[] { new ObjectReferenceKeyframe { time = 0, value = Value } });
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return other is ObjectPropAction action && Prop.Equals(action.Prop) && Equals(Value, action.Value);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            if (Prop.TargetObject == null) return StaticApplyResult.Retain;

            var serializedObject = new SerializedObject(Prop.TargetObject);
            var property = serializedObject.FindProperty(Prop.PropertyName);
            if (property is not { propertyType: SerializedPropertyType.ObjectReference })
                return StaticApplyResult.Retain;

            property.objectReferenceValue = Value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return StaticApplyResult.Applied;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ObjectPropAction other &&
                                                  Prop.Equals(other.Prop) && Equals(Value, other.Value));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Prop, Value);
        }
    }
}