#nullable enable

using System;

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public sealed class ParameterExpression : IExpression
    {
        public enum ConditionMode
        {
            GreaterThan,
            LessThan
        }

        public string ParameterName { get; set; }
        public ConditionMode Mode { get; set; }
        public float Threshold { get; set; }

        public ParameterExpression(
            string parameterName,
            float? threshold = null,
            ConditionMode mode = ConditionMode.GreaterThan)
        {
            ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
            Threshold = threshold ?? 0.5f;
            Mode = mode;
        }

        public void Walk(ExpressionVisitor visitor)
        {
            // leaf node
        }

        public override string ToString()
        {
            var op = Mode == ConditionMode.GreaterThan ? ">" : "<";
            return $"{ParameterName} {op} {Threshold}";
        }

        public override bool Equals(object obj)
        {
            return obj is ParameterExpression other &&
                   ParameterName == other.ParameterName &&
                   Mode == other.Mode &&
                   Threshold.Equals(other.Threshold);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (ParameterName?.GetHashCode() ?? 0);
                hash = hash * 31 + Mode.GetHashCode();
                hash = hash * 31 + Threshold.GetHashCode();
                return hash;
            }
        }
    }
}