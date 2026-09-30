#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    internal static class AssignInitialGroupStatesTransform
    {
        internal static void Apply(UnityBlendTreeBackend backend, List<EffectGroup> groups)
        {
            var context = new ExpressionEvaluationContext(backend.GetParameterInitialValue);

            foreach (var group in groups)
            {
                foreach (var (node, index) in group.Nodes.Select((n, i) => (n, i)))
                {
                    if (node.Expression.Evaluate(context))
                    {
                        group.DefaultNode = index;
                    }
                }

                if (group.DefaultNode.HasValue)
                {
                    var defaultNode = group.DefaultNode.Value;
                    foreach (var effectGroup in group.Nodes[defaultNode].Effects.GroupBy(a => a.TargetKey))
                    {
                        backend.ApplyBaseState(effectGroup.First(), true);
                    }
                }
                else
                {
                    foreach (var effectGroup in group.Nodes.SelectMany(n => n.Effects).GroupBy(a => a.TargetKey))
                    {
                        backend.ApplyBaseState(effectGroup.First(), false);
                    }
                }
            }
        }
    }
}
