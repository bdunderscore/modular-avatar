using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public sealed class NullAction : IAction
    {
        public NullAction(object? targetKey = null)
        {
            TargetKey = targetKey ?? new object();
        }

        public object TargetKey { get; }

        public void ToMotion(BakeContext context, VirtualClip motion)
        {
            // no-op
        }

        private bool Equals(NullAction other)
        {
            return Equals(TargetKey, other.TargetKey);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is NullAction other && Equals(other));
        }

        public override int GetHashCode()
        {
            return TargetKey != null ? TargetKey.GetHashCode() : 0;
        }

        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            // no-op
        }

        public override string ToString()
        {
            return "<noop>";
        }
    }
}