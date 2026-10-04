#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class ReactiveMoveAction : IAction
    {
        internal Transform ToMove { get; }
        internal Transform? WhereTo { get; }
        internal bool FixToWorld { get; }
        internal bool SetsPosition { get; }
        internal bool SetsRotation { get; }
        internal bool SetsScale { get; }
        internal float TransitionTime { get; }
        public object TargetKey { get; }

        internal ReactiveMoveAction(
            Transform toMove,
            Transform? whereTo,
            bool fixToWorld,
            bool setsPosition,
            bool setsRotation,
            bool setsScale,
            float transitionTime
        )
        {
            ToMove = toMove;
            WhereTo = whereTo;
            FixToWorld = fixToWorld;
            SetsPosition = setsPosition;
            SetsRotation = setsRotation;
            SetsScale = setsScale;
            TransitionTime = transitionTime;
            TargetKey = new ReactiveMoveTarget(toMove);
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return Equals(other);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            return StaticApplyResult.Retain;
        }

        private bool Equals(ReactiveMoveAction other)
        {
            return Equals(ToMove, other.ToMove) &&
                   Equals(WhereTo, other.WhereTo) &&
                   SetsPosition == other.SetsPosition &&
                   SetsRotation == other.SetsRotation &&
                   SetsScale == other.SetsScale &&
                   TransitionTime == other.TransitionTime;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ReactiveMoveAction other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ToMove, WhereTo, SetsPosition, SetsRotation, SetsScale, TransitionTime);
        }
    }
}