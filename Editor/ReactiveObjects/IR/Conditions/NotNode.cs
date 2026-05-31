#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public sealed class NotNode : IExpression
    {
        private IExpression _inner = new Constant(false);

        public NotNode(IExpression inner)
        {
            Inner = inner;
        }

        public IExpression Inner
        {
            get => _inner;
            set => _inner = value;
        }

        public override string ToString()
        {
            return $"NOT({Inner})";
        }

        public IExpression DeepClone()
        {
            return new NotNode(Inner.DeepClone());
        }

        public void Walk(ExpressionVisitor visitor)
        {
            visitor(ref _inner);
        }

        public override bool Equals(object obj)
        {
            return obj is NotNode other && Equals(Inner, other.Inner);
        }

        public override int GetHashCode()
        {
            return Inner?.GetHashCode() ?? 0;
        }
    }
}