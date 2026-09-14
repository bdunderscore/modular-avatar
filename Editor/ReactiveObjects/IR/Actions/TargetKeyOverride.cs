#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class TargetKeyOverride : IAction
    {
        public object TargetKey { get; }
        internal ImmutableArray<IAction> Actions { get; }

        internal TargetKeyOverride(object targetKey, IReadOnlyList<IAction> actions)
        {
            TargetKey = targetKey;
            Actions = actions.ToImmutableArray();
        }

        public bool ApproximatelyEqual(IAction other)
        {
            if (other is not TargetKeyOverride overrideAction
                || !Equals(TargetKey, overrideAction.TargetKey)
                || Actions.Length != overrideAction.Actions.Length)
            {
                return false;
            }

            for (var i = 0; i < Actions.Length; i++)
            {
                if (!Actions[i].ApproximatelyEqual(overrideAction.Actions[i])) return false;
            }

            return true;
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            return StaticApplyResult.Retain;
        }

        private bool Equals(TargetKeyOverride other)
        {
            if (!Equals(TargetKey, other.TargetKey) || Actions.Length != other.Actions.Length) return false;

            for (var i = 0; i < Actions.Length; i++)
            {
                if (!Equals(Actions[i], other.Actions[i])) return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is TargetKeyOverride other && Equals(other));
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(TargetKey);
            foreach (var action in Actions) hash.Add(action);
            return hash.ToHashCode();
        }
    }
}