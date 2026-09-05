#nullable enable

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal readonly struct PropertyTarget
    {
        public Object TargetObject { get; }
        public string PropertyName { get; }

        public PropertyTarget(Object targetObject, string propertyName)
        {
            TargetObject = targetObject;
            PropertyName = propertyName;
        }

        public static PropertyTarget ForObjectActive(GameObject targetObject)
        {
            return new PropertyTarget(targetObject, "m_IsActive");
        }

        private bool Equals(PropertyTarget other)
        {
            return Equals(TargetObject, other.TargetObject) && PropertyName == other.PropertyName;
        }

        public override bool Equals(object? obj)
        {
            return obj is PropertyTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TargetObject != null ? TargetObject.GetHashCode() : 0, PropertyName);
        }

        public override string ToString()
        {
            return $"{TargetObject}.{PropertyName}";
        }
    }
}