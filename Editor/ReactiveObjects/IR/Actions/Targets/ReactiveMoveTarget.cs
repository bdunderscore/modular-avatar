#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal enum ReactiveMoveChannels
    {
        Position = 1,
        Rotation = 2,
        Scale = 4
    }

    internal sealed class ReactiveMoveTarget
    {
        internal Transform ToMove { get; }
        internal ReactiveMoveChannels Channel { get; }

        internal ReactiveMoveTarget(Transform toMove, ReactiveMoveChannels channel)
        {
            ToMove = toMove;
            Channel = channel;
        }

        private bool Equals(ReactiveMoveTarget other)
        {
            return Equals(ToMove, other.ToMove) && Channel == other.Channel;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ReactiveMoveTarget other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ToMove, Channel);
        }

        public override string ToString()
        {
            return ToMove != null ? $"{ToMove.name}:{Channel}" : "(missing Reactive Move target)";
        }
    }
}