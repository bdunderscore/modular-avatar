#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public delegate void ExpressionVisitor(ref IExpression expr);

    /// <summary>
    ///     Represents a boolean expression in the reaction graph.
    /// </summary>
    public interface IExpression
    {
        public void Walk(ExpressionVisitor visitor);
    }
}