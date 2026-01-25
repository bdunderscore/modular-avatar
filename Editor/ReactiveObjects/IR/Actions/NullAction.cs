using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public sealed class NullAction : IAction
    {
        public object TargetKey => "<noop>";

        public void ToMotion(BakeContext context, VirtualClip motion)
        {
            // no-op
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return true;
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }
    }
}