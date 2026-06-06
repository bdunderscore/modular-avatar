#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    /// <summary>
    ///     A reaction node is a point which can be enabled or disabled, has a particular priority, and
    ///     triggers some set of reactions (potentially including enabling/disabling objects)
    /// </summary>
    public class ReactionNode
    {
        public int Priority { get; set; } = 0;
        public List<IAction> Effects = new();
        public IExpression Expression { get; set; }

        public ReactionNode(IExpression expression, IAction effect)
        {
            Expression = expression;
            Effects.Add(effect);
        }
    }
}
