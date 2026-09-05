#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class AnimatedProperty
    {
        public object TargetKey { get; }

        public List<ReactionRule> actionGroups = new();

        public AnimatedProperty(object targetKey)
        {
            TargetKey = targetKey;
        }

        protected bool Equals(AnimatedProperty other)
        {
            return actionGroups.SequenceEqual(other.actionGroups) && TargetKey.Equals(other.TargetKey);
        }

        public override bool Equals(object? obj)
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

            return HashCode.Combine(actionGroupHash, TargetKey);
        }
    }
}