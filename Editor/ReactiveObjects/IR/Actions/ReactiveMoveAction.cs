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
        internal ReactiveMoveChannels Channel { get; }
        internal bool SetsPosition => Channel == ReactiveMoveChannels.Position;
        internal bool SetsRotation => Channel == ReactiveMoveChannels.Rotation;
        internal bool SetsScale => Channel == ReactiveMoveChannels.Scale;
        internal float TransitionTime { get; }
        public object TargetKey { get; }

        internal ReactiveMoveAction(
            Transform toMove,
            Transform? whereTo,
            bool fixToWorld,
            ReactiveMoveChannels channel,
            float transitionTime
        )
        {
            ToMove = toMove;
            WhereTo = fixToWorld ? null : whereTo;
            FixToWorld = fixToWorld;
            Channel = channel;
            TransitionTime = transitionTime;
            TargetKey = new ReactiveMoveTarget(toMove, channel);
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
                   FixToWorld == other.FixToWorld &&
                   Channel == other.Channel &&
                   TransitionTime == other.TransitionTime;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ReactiveMoveAction other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ToMove, WhereTo, FixToWorld, Channel, TransitionTime);
        }
    }
}