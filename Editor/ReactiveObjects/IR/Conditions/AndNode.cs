#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public sealed class AndNode : IExpression
    {
        public List<IExpression> Children { get; set; } = new();

        public override string ToString()
        {
            return $"AND({string.Join(", ", Children)})";
        }

        public AndNode(params IExpression[] children)
        {
            Children = children.ToList();
        }

        public IExpression DeepClone()
        {
            return new AndNode(Children.Select(c => c.DeepClone()).ToArray());
        }

        public void Walk(ExpressionVisitor visitor)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                var tmp = Children[i];
                visitor(ref tmp);
                Children[i] = tmp;
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is AndNode other && Children.SequenceEqual(other.Children);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var child in Children)
                {
                    hash = hash * 31 + (child?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }
    }
}
