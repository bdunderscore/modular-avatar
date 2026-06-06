#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    internal static class AssignInitialStates
    {
        public static void ProcessGraph(BakeContext context, ReactionGraph groups)
        {
            Dictionary<ParameterTarget, bool> currentValues = new();
            Dictionary<ParameterTarget, bool> nextValues = new();

            var drivers = groups.Nodes
                .SelectMany(n =>
                    n.Effects.OfType<DriveInternalParameter>().Select(e => (expr: n.Expression, effect: e)))
                .ToList();

            var changed = false;
            var iterationLimit = 10;

            do
            {
                changed = false;

                foreach (var (expr, driver) in drivers)
                {
                    var target = new ParameterTarget(driver.ParameterName);
                    bool? state = null;

                    var exprValue = VirtualEvaluate(context, currentValues, expr);
                    if (exprValue)
                    {
                        state = driver.State;
                    }

                    if (state.HasValue)
                    {
                        nextValues[target] = state.Value;
                    }
                }

                if (currentValues.Count != nextValues.Count)
                {
                    changed = true;
                }
                else
                {
                    foreach (var (k, v) in nextValues)
                    {
                        if (currentValues.GetValueOrDefault(k) != v)
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                currentValues = nextValues;
                nextValues = new Dictionary<ParameterTarget, bool>();
            } while (changed && --iterationLimit > 0);

            foreach (var (k, v) in currentValues)
            {
                context.SetParameter(k.ParameterName, v ? 1.0f : 0.0f);
            }
        }

        public static void ProcessGroups(BakeContext context, List<EffectGroup> groups)
        {
            // We've baked the values into the context now, so we can leave this dictionary empty
            Dictionary<ParameterTarget, bool> currentValues = new();

            foreach (var group in groups)
            {
                foreach (var (node, index) in group.Nodes.Select((n, i) => (n, i)))
                {
                    if (VirtualEvaluate(context, currentValues, node.Expression))
                    {
                        group.DefaultNode = index;
                    }
                }

                if (group.DefaultNode.HasValue)
                {
                    var defaultNode = group.DefaultNode.Value;
                    group.Nodes[defaultNode].Effects.First(e => e.TargetKey.Equals(group.TargetKey))
                        .SetBaseState(context, true);
                }
                else
                {
                    group.Nodes.SelectMany(n => n.Effects)
                        .First(e => e.TargetKey.Equals(group.TargetKey))
                        .SetBaseState(context, false);
                }
            }

            // TODO - update:
            // base unity state (must contain the state driven by the current active motion/effect)
        }

        private static bool VirtualEvaluate(
            BakeContext bakeContext,
            Dictionary<ParameterTarget, bool> stateOverrides,
            IExpression expr
        )
        {
            switch (expr)
            {
                case InternalParameterCondition ipc:
                {
                    if (stateOverrides.TryGetValue(new ParameterTarget(ipc.ParameterName), out var state))
                    {
                        return state;
                    }

                    return bakeContext.GetParameterInitialValue(ipc.ParameterName) > 0.5f;
                }
                case AndNode and:
                    return and.Children.All(e => VirtualEvaluate(bakeContext, stateOverrides, e));
                case OrNode or:
                    return or.Children.Any(e => VirtualEvaluate(bakeContext, stateOverrides, e));
                case NotNode not:
                    return !VirtualEvaluate(bakeContext, stateOverrides, not.Inner);
                case Constant c:
                    return c.Value;
                default:
                    return false;
            }
        }
    }
}
