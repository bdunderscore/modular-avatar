#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class DriveInternalParameter : IAction
    {
        public object TargetKey => new InternalParameterTarget(ParameterName);
        public string ParameterName { get; set; }
        public bool State { get; set; }

        public DriveInternalParameter(string parameterName, bool state)
        {
            ParameterName = parameterName;
            State = state;
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return Equals(other);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            return StaticApplyResult.Retain;
        }

        private bool Equals(DriveInternalParameter other)
        {
            return ParameterName == other.ParameterName && State == other.State;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is DriveInternalParameter other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ParameterName, State);
        }

        public override string ToString() => $"DriveInternalParameter({ParameterName}, {State})";
    }
}
