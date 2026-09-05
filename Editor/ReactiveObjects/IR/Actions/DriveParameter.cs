#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class DriveParameter : IAction
    {
        public object TargetKey => new ParameterTarget(ParameterName);
        public string ParameterName { get; set; }
        public float Value { get; set; }

        public DriveParameter(string parameterName, float value)
        {
            ParameterName = parameterName;
            Value = value;
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return Equals(other);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            return StaticApplyResult.Retain;
        }

        private bool Equals(DriveParameter other)
        {
            return ParameterName == other.ParameterName && Value.Equals(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is DriveParameter other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ParameterName, Value);
        }

        public override string ToString() => $"DriveParameter({ParameterName}, {Value})";
    }
}
