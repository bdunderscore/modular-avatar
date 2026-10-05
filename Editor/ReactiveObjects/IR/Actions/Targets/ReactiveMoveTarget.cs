#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{

    internal sealed class ReactiveMoveTarget
    {
        internal Transform ToMove { get; }
        internal ReactiveMoveTarget(Transform toMove)
        {
            ToMove = toMove;
        }

        private bool Equals(ReactiveMoveTarget other)
        {
            return Equals(ToMove, other.ToMove);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ReactiveMoveTarget other && Equals(other));
        }

        public override int GetHashCode()
        {
            return ToMove.GetHashCode();
        }

        public override string ToString()
        {
            return ToMove != null ? ToMove.name : "(missing Reactive Move target)";
        }
    }
}