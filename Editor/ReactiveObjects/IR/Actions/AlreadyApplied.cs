#nullable enable

using System;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class AlreadyApplied : IAction
    {
        public IAction Inner { get; }

        public object TargetKey => Inner.TargetKey;

        public AlreadyApplied(IAction inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return other is AlreadyApplied applied && Inner.ApproximatelyEqual(applied.Inner);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            return StaticApplyResult.Applied;
        }
    }
}