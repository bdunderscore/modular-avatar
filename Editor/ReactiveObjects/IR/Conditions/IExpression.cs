#nullable enable

using System;

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    internal delegate void ExpressionVisitor(ref IExpression expr);

    /// <summary>
    ///     Represents a boolean expression in the reaction graph.
    /// </summary>
    internal interface IExpression
    {
        /// <summary>
       /// Evaluate the expression based on the provided parameter lookup callback
       /// </summary>
       /// <param name="getParameter"></param>
       /// <returns></returns>
        public bool Evaluate(Func<string, float> getParameter);
        /// <summary>
       ///  Visits all nodes in the expression
       /// </summary>
       /// <param name="visitor"></param>
        public void Walk(ExpressionVisitor visitor);
        public IExpression DeepClone();
    }
}