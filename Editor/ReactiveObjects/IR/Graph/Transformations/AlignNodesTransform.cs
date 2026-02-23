using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Given a single ReactionGraph, converts into a series of delay phases, aligning things so that all inputs
    ///     flow through the same number of ReactionNodes before reaching an externally visible effect.
    ///     Here, "externally visible effect" means any effect that is not a DriveInternalParameter effect.
    ///     In order to align things, we insert delay nodes. We assume that there are no loops in the graph (these should be
    ///     handled by the prior BreakLoopsTransform).
    /// </summary>
    internal class AlignNodesTransform
    {
        public static List<EffectGroup> Apply(BakeContext context, ReactionGraph graph)
        {
            // TODO: group multiple effects that always activate together into the same condition nodes

            var byEffect = graph.Nodes.SelectMany(n => n.Effects.Select(e => (n, e)))
                .GroupBy(pair => pair.e.TargetKey)
                .Select(g => new EffectGroup(context, g.Key, g.Select(p => p.n).ToList()))
                .ToDictionary(kv => kv.TargetKey, kv => kv);

            // Our high level algorithm is as follows: The EffectGroups form a directed graph, to which we will assign
            // depths. All nodes with visible effects have depth zero. A node's inputs must all be the same depth,
            // and that differs from the node by a constant value (1 for 2-or-less condition nodes, 2 for others)
            //
            // We'll start by indexing the inbound and outbound edges for each node.

            // node -> list of all internal ParameterExpression()s it depends on
            Dictionary<object, List<ParameterTarget>> nodeToSourceExpressions = new();
            // parameter node -> list of all internal nodes which depends on this expression
            Dictionary<ParameterTarget, List<object>> nodeToTargetExpressions = new();

            foreach (var group in byEffect.Values)
            {
                var srcExpressions = GetInternalParameterReferences(group)
                    .Select(pn => new ParameterTarget(pn)).ToList();
                nodeToSourceExpressions[group.TargetKey] = srcExpressions;

                foreach (var pn in srcExpressions)
                {
                    if (!nodeToTargetExpressions.TryGetValue(pn, out var targets))
                    {
                        targets = new List<object>();
                        nodeToTargetExpressions[pn] = targets;
                    }

                    targets.Add(group.TargetKey);
                }
            }

            // Now, for nodes which have an external effect, we'll assign depth zero.
            Queue<EffectGroup> pending = new();
            HashSet<EffectGroup> enqueued = new();

            foreach (var group in byEffect.Values.Where(g =>
                         g.TargetKey is not ParameterTarget pt || !nodeToTargetExpressions.ContainsKey(pt))
                    )
            {
                AssignDepth(group, 0);
            }

            // Now we go through the remaining until we have nothing left to assign
            while (pending.Count > 0)
            {
                var group = pending.Dequeue();
                if (group.TargetKey is not ParameterTarget pt
                    || !nodeToTargetExpressions.TryGetValue(pt, out var downstreamNodes))
                {
                    continue;
                }

                var downstreamDepth = downstreamNodes.Select(key => byEffect[key])
                    .Max(node => node.Depth! + node.Latency) ?? 0;

                AssignDepth(group, downstreamDepth);
            }

            void AssignDepth(EffectGroup group, int depth)
            {
                enqueued.Add(group);
                group.Depth = depth;
                foreach (var node in nodeToSourceExpressions[group.TargetKey])
                {
                    // If one of our source nodes has all of its downstream edges set, we can now process it
                    var upstream = byEffect[node];
                    if (!enqueued.Contains(upstream) &&
                        nodeToTargetExpressions[node].All(t => byEffect[t].Depth.HasValue))
                    {
                        pending.Enqueue(upstream);
                        enqueued.Add(upstream);
                    }
                }
            }

            // Now assign delay nodes. We do this where the latency gap is higher on an edge than it needs to be
            foreach (var group in byEffect.Values.ToList())
            {
                if (!group.Depth.HasValue) throw new Exception("Failed to assign depth to node " + group.TargetKey);

                var parameters = GetInternalParameterReferences(group);
                var targetDepth = group.Latency + group.Depth.Value;

                Dictionary<string, string> replacements = new();
                foreach (var parameter in parameters)
                {
                    var paramNode = byEffect[new ParameterTarget(parameter)];
                    if (!paramNode.Depth.HasValue) throw new Exception("Failed to assign depth to node " + parameter);
                    if (paramNode.Depth == targetDepth) continue;
                    if (paramNode.Depth < targetDepth)
                        throw new Exception("Invalid edge from " + group.TargetKey + " to " + parameter);

                    var neededDelay = paramNode.Depth.Value - targetDepth;
                    var finalDelayParam = parameter + "$d" + neededDelay;
                    for (var i = neededDelay; i > 0; i--)
                    {
                        var priorNode = i == 1 ? parameter : parameter + "$d" + (i - 1);
                        var delayParam = new ParameterTarget(parameter + "$d" + i);
                        if (byEffect.ContainsKey(delayParam)) break;

                        byEffect[delayParam] =
                            BuildDelayNode(context, priorNode, delayParam.ParameterName, paramNode.Depth.Value - i);
                    }

                    replacements[parameter] = finalDelayParam;
                }

                if (replacements.Count > 0)
                {
                    ApplyReplacements(group, replacements);
                }
            }

            // Do a final check that everything is correct
            foreach (var group in byEffect.Values)
            {
                var parameters = GetInternalParameterReferences(group);
                if (!group.Depth.HasValue) throw new Exception("Failed to assign depth to group " + group.TargetKey);
                foreach (var parameter in parameters)
                {
                    var paramNode = byEffect[new ParameterTarget(parameter)];
                    if (!paramNode.Depth.HasValue) throw new Exception("Failed to assign depth to node " + parameter);
                    if (paramNode.Depth.Value != group.Depth.Value + group.Latency)
                        throw new Exception("Invalid edge from " + group.TargetKey + " to " + parameter);
                }
            }

            return byEffect.Values.ToList();
        }

        private static EffectGroup BuildDelayNode(BakeContext context, string priorNode, string delayParameter,
            int depth)
        {
            var onFalse = new ReactionNode(
                new Constant(true),
                new DriveInternalParameter(delayParameter, false)
            );
            var onTrue = new ReactionNode(
                new InternalParameterCondition(priorNode),
                new DriveInternalParameter(delayParameter, true)
            );

            var group = new EffectGroup(context,
                new ParameterTarget(delayParameter),
                new List<ReactionNode> { onFalse, onTrue }
            );
            group.Depth = depth;

            return group;
        }

        private static void ApplyReplacements(EffectGroup group, Dictionary<string, string> replacements)
        {
            foreach (var node in group.Nodes)
            {
                var exp = node.Expression;
                Visit(ref exp);
                node.Expression = exp;
            }

            void Visit(ref IExpression expr)
            {
                if (expr is InternalParameterCondition ipc)
                {
                    if (replacements.TryGetValue(ipc.ParameterName, out var replacement))
                    {
                        ipc.ParameterName = replacement;
                    }
                }
                else
                {
                    expr.Walk(Visit);
                }
            }
        }

        private static HashSet<string> GetInternalParameterReferences(EffectGroup group)
        {
            HashSet<string> references = new();

            foreach (var node in group.Nodes)
            {
                var exp = node.Expression;
                Visit(ref exp);
            }

            return references;

            void Visit(ref IExpression expr)
            {
                if (expr is InternalParameterCondition condition) references.Add(condition.ParameterName);
                else expr.Walk(Visit);
            }
        }

        private static bool DependsOnInternalParameter(EffectGroup group)
        {
            var hasInternalParameter = false;

            foreach (var node in group.Nodes)
            {
                var exp = node.Expression;
                Visit(ref exp);
            }

            return hasInternalParameter;

            void Visit(ref IExpression expr)
            {
                if (hasInternalParameter) return;
                if (expr is InternalParameterCondition) hasInternalParameter = true;
                else expr.Walk(Visit);
            }
        }
    }
}