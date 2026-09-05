#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class DriveActiveState : IAction
    {
        public object TargetKey => new ObjectActiveTarget(Target);
        public GameObject Target { get; }
        public bool Active { get; }

        public DriveActiveState(GameObject target, bool active)
        {
            Target = target;
            Active = active;
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return Equals(other);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            if (Target == null) return StaticApplyResult.Retain;

            Target.SetActive(Active);
            return StaticApplyResult.Applied;
        }

        private bool Equals(DriveActiveState other)
        {
            return Equals(Target, other.Target) && Active == other.Active;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is DriveActiveState other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Target != null ? Target.GetHashCode() : 0, Active);
        }

        public override string ToString() => $"DriveActiveState({Target.name}, {Active})";
    }
}
