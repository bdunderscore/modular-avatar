#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Performs action forwarding. For each node whose expression references ObjectActiveState expressions,
    ///     if there are one or two nodes driving that ObjectActiveState, we can replace that ObjectActiveState
    ///     expression with a boolean condition built from the expressions of the driving node.
    ///     Each ObjectActiveState is processed independently by (object, StateMode) pair.
    /// </summary>
    public static class ForwardObjectActiveDriversTransform
    {
        public static void Apply(ReactionGraph graph)
        {
            // Map each object to the driver nodes that manipulate its active state
            var driversByObject = graph.Nodes
                .SelectMany((n, idx) => n.Effects.OfType<DriveActiveState>().Select(d => (idx, node: n, drive: d)))
                .GroupBy(x => x.drive.Target)
                .ToDictionary(g => g.Key, g => g.Select(x => (x.idx, x.node, x.drive)).ToList());

            foreach (var node in graph.Nodes)
            {
                var oasTargets = FindAllObjectActiveStates(node.Expression);

                foreach (var targetOas in oasTargets)
                {
                    if (!driversByObject.TryGetValue(targetOas.TargetObject, out var drivers) || drivers.Count == 0)
                        continue;

                    // Only forward when at most two drivers, except for the "all opposite" shortcut
                    var driverCount = drivers.Count;
                    var wantActive = targetOas.StateMode == ObjectActiveState.State.Active;
                    var wantInactive = targetOas.StateMode == ObjectActiveState.State.Inactive;
                    var wantNotDriven = targetOas.StateMode == ObjectActiveState.State.NotDriven;

                    IExpression? replacement = null;

                    if (wantNotDriven)
                    {
                        if (driverCount <= 2)
                        {
                            var exprs = drivers.Select(d => d.node.Expression).ToList();
                            replacement = exprs.Count == 1
                                ? new NotNode(exprs[0])
                                : new NotNode(new OrNode(exprs[0], exprs[1]));
                        }
                    }
                    else
                    {
                        // Determine driver alignment with desired state
                        var alignments = drivers.Select(d => (nodeExpr: d.node.Expression,
                            aligns: d.drive.Active == wantActive || (wantInactive && !d.drive.Active))).ToList();

                        var allOpposite = alignments.All(a => !a.aligns);
                        if (allOpposite)
                        {
                            // Always collapse to false if every driver drives the opposite state
                            replacement = new Constant(false);
                        }
                        else if (driverCount <= 2)
                        {
                            replacement = BuildReplacementForTwoOrFewer(alignments);
                        }
                    }

                    if (replacement == null) continue;

                    node.Expression = ReplaceObjectActiveState(node.Expression, targetOas, replacement);
                }
            }
        }

        private static List<ObjectActiveState> FindAllObjectActiveStates(IExpression expr)
        {
            var states = new List<ObjectActiveState>();

            void Visit(ref IExpression e)
            {
                if (e is ObjectActiveState oas)
                {
                    states.Add(oas);
                    return;
                }

                e.Walk(Visit);
            }

            var tmp = expr;
            Visit(ref tmp);
            return states;
        }

        private static IExpression BuildReplacementForTwoOrFewer(List<(IExpression nodeExpr, bool aligns)> aligns)
        {
            if (aligns.Count == 1)
            {
                return aligns[0].aligns ? aligns[0].nodeExpr : new Constant(false);
            }

            // count == 2 here
            var a1 = aligns[0];
            var a2 = aligns[1];

            if (!a1.aligns && !a2.aligns) return new Constant(false);
            if (a1.aligns && a2.aligns) return new OrNode(a1.nodeExpr, a2.nodeExpr);
            if (a1.aligns && !a2.aligns) return new AndNode(a1.nodeExpr, new NotNode(a2.nodeExpr));
            // (!a1.aligns && a2.aligns)
            return a2.nodeExpr;
        }

        private static IExpression ReplaceObjectActiveState(IExpression expr, ObjectActiveState target,
            IExpression replacement)
        {
            void Visit(ref IExpression e)
            {
                e.Walk(Visit);
                if (e is ObjectActiveState oas && SameTarget(oas, target)) e = replacement;
            }

            var tmp = expr;
            Visit(ref tmp);
            return tmp;
        }

        private static bool SameTarget(ObjectActiveState a, ObjectActiveState b)
        {
            return a.TargetObject == b.TargetObject && a.StateMode == b.StateMode;
        }
    }
}