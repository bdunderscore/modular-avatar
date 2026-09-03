#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class NullAction : IAction
    {
        public NullAction(object? targetKey = null)
        {
            TargetKey = targetKey ?? new object();
        }

        public object TargetKey { get; }

        private bool Equals(NullAction other) => Equals(TargetKey, other.TargetKey);

        public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is NullAction other && Equals(other));

        public override int GetHashCode() => TargetKey.GetHashCode();

        public override string ToString() => "<noop>";
    }
}
