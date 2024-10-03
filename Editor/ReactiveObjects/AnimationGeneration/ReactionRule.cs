using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactionRule
    {
        public ReactionRule(TargetProp key, float value)
            : this(key, (object)value) { }
            
        public  ReactionRule(TargetProp key, UnityEngine.Object value)
            : this(key, (object)value) { }
            
        private ReactionRule(TargetProp key, object value)
        {
            TargetProp = key;

            ControllingConditions = new();
                
            Value = value;
        }

        public TargetProp TargetProp;
        public object Value;

        public Component ControllingObject;

        public List<ControlCondition> ControllingConditions;

        public bool InitiallyActive =>
            ((ControllingConditions.Count == 0) || ControllingConditions.All(c => c.InitiallyActive)) ^ Inverted;
        public bool IsDelete;

        public bool Inverted;

        public bool IsConstant => ControllingConditions.Count == 0
                                  || ControllingConditions.All(c => c.IsConstant)
                                  || ControllingConditions.Any(c => c.IsConstant && !c.InitiallyActive);
        public bool IsConstantOn => IsConstant && InitiallyActive;

        public override string ToString()
        {
            return $"AGK: {TargetProp}={Value}";
        }

        public bool TryMerge(ReactionRule other)
        {
            if (!TargetProp.Equals(other.TargetProp)) return false;
                
            // Value checks
            if (Value == other.Value) { /* objects match */ }
            else if (Value is float a && other.Value is float b)
            {
                if (Mathf.Abs(a - b) > 0.001f) return false;
            }
            else return false;
            if (!ControllingConditions.SequenceEqual(other.ControllingConditions)) return false;
            if (IsDelete || other.IsDelete) return false;

            return true;
        }
    }
}