#nullable enable


namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    internal delegate void ExpressionVisitor(ref IExpression expr);

    /// <summary>
    ///     Represents a boolean expression in the reaction graph.
    /// </summary>
    internal interface IExpression
    {
        /// <summary>
        ///     Evaluate the expression in the provided context.
        /// </summary>
        public bool Evaluate(ExpressionEvaluationContext context);
        /// <summary>
       ///  Visits all nodes in the expression
       /// </summary>
       /// <param name="visitor"></param>
        public void Walk(ExpressionVisitor visitor);
        public IExpression DeepClone();
    }
}