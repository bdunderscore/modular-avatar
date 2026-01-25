using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    public sealed class ObjectActiveTarget
    {
        public GameObject Target { get; }

        public ObjectActiveTarget(GameObject target)
        {
            Target = target;
        }

        public override string ToString()
        {
            return Target.name;
        }

        private bool Equals(ObjectActiveTarget other)
        {
            return Equals(Target, other.Target);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is ObjectActiveTarget other && Equals(other));
        }

        public override int GetHashCode()
        {
            return Target != null ? Target.GetHashCode() : 0;
        }
    }
}