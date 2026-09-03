#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class PropAction : IAction
    {
        public PropAction(TargetProp prop, object? value = null)
        {
            Prop = prop;
            Value = value;
        }

        internal TargetProp Prop { get; }
        internal object? Value { get; }
        public object TargetKey => Prop;
    }
}
