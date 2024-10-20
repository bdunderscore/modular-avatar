using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactionRule
    {
        public ReactionRule(TargetProp key, float value)
            : this(key, (object)value) { }

        public ReactionRule(TargetProp key, Object value)
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

        public bool Inverted;

        public bool IsConstant => ControllingConditions.Count == 0
                                  || ControllingConditions.All(c => c.IsConstant)
                                  || ControllingConditions.Any(c => c.IsConstant && !c.InitiallyActive);

        public bool IsConstantActive => IsConstant && InitiallyActive ^ Inverted;
        
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

            return true;
        }

        protected bool Equals(ReactionRule other)
        {
            return TargetProp.Equals(other.TargetProp)
                   && Equals(Value, other.Value)
                   && Equals(ControllingObject, other.ControllingObject)
                   && ControllingConditions.SequenceEqual(other.ControllingConditions)
                   && Inverted == other.Inverted;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ReactionRule)obj);
        }

        public override int GetHashCode()
        {
            var ccHash = 0;
            foreach (var cc in ControllingConditions)
            {
                ccHash = HashCode.Combine(ccHash, cc);
            }

            return HashCode.Combine(TargetProp, Value, ControllingObject, ccHash, Inverted);
        }
    }
}