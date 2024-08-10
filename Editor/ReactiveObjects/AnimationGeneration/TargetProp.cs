using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    partial class ReactiveObjectPass
    {
        struct TargetProp
        {
            public Object TargetObject;
            public string PropertyName;

            public bool Equals(TargetProp other)
            {
                return Equals(TargetObject, other.TargetObject) && PropertyName == other.PropertyName;
            }

            public override bool Equals(object obj)
            {
                return obj is TargetProp other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (TargetObject != null ? TargetObject.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (PropertyName != null ? PropertyName.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
    }
}