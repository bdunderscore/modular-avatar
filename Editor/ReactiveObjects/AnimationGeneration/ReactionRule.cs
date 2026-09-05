#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactionRule
    {
        public ReactionRule(IAction action)
        {
            Action = action;
            ControllingConditions = new();
        }

        public IAction Action;

        public Component? ControllingObject;

        public List<ControlCondition> ControllingConditions;

        public bool InitiallyActive =>
            ((ControllingConditions.Count == 0) || ControllingConditions.All(c => c.InitiallyActive)) ^ Inverted;

        public bool Inverted;

        public bool IsConstant => ControllingConditions.Count == 0
                                  || ControllingConditions.All(c => c.IsConstant)
                                  || ControllingConditions.Any(c => c.IsConstant && !c.InitiallyActive);

        public bool IsConstantActive => IsConstant && InitiallyActive;

        public override string ToString()
        {
            return $"AGK: {Action}";
        }

        public bool TryMerge(ReactionRule other)
        {
            if (!Equals(Action.TargetKey, other.Action.TargetKey)) return false;
            if (!Action.ApproximatelyEqual(other.Action)) return false;

            return ControllingConditions.SequenceEqual(other.ControllingConditions);
        }

        protected bool Equals(ReactionRule other)
        {
            return Equals(Action, other.Action)
                   && Equals(ControllingObject, other.ControllingObject)
                   && ControllingConditions.SequenceEqual(other.ControllingConditions)
                   && Inverted == other.Inverted;
        }

        public override bool Equals(object? obj)
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

            return HashCode.Combine(Action, ControllingObject, ccHash, Inverted);
        }
    }
}
