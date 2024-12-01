using System;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class AnimatedProperty
    {
        public TargetProp TargetProp { get; }

        public object currentState;

        // Objects which trigger deletion of this shape key. 
        public List<ReactionRule> actionGroups = new List<ReactionRule>();

        public AnimatedProperty(TargetProp key, float currentState)
        {
            TargetProp = key;
            this.currentState = currentState;
        }
            
        public AnimatedProperty(TargetProp key, Object currentState)
        {
            TargetProp = key;
            this.currentState = currentState;
        }

        protected bool Equals(AnimatedProperty other)
        {
            return Equals(currentState, other.currentState) && actionGroups.SequenceEqual(other.actionGroups) &&
                   TargetProp.Equals(other.TargetProp);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AnimatedProperty)obj);
        }

        public override int GetHashCode()
        {
            var actionGroupHash = 0;
            foreach (var ag in actionGroups)
            {
                actionGroupHash = HashCode.Combine(actionGroupHash, ag);
            }

            return HashCode.Combine(currentState, actionGroupHash, TargetProp);
        }
    }
}