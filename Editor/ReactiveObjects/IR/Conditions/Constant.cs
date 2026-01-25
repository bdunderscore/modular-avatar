#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public sealed class Constant : IExpression
    {
        public bool Value { get; set; }

        public Constant(bool value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public void Walk(ExpressionVisitor visitor)
        {
            // leaf node
        }

        public override bool Equals(object obj)
        {
            return obj is Constant other && Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}