using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

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
        private readonly BakeContext context;

        // Edges from high delay to low delay - ie, from data source to sink
        private readonly Dictionary<object, HashSet<object>> _fwdEdges = new();
        private readonly Dictionary<object, HashSet<object>> _revEdges = new();

        // Tracks how many downstream neighbors have been depth-assigned for each node.
        private readonly Dictionary<object, int> _visitedDownstream = new();

        // All nodes not yet fully processed (all downstream depth-assigned). Populated in CreateNode,
        // drained as nodes are enqueued during TopoAssignDepths; anything remaining after is in a cycle.
        private readonly HashSet<object> _unprocessedNodes = new();
        private readonly Dictionary<object, int> _assignedDepths = new();
        private readonly HashSet<string> _createdDelayParameters = new();

        private AlignNodesTransform(BakeContext context)
        {
            this.context = context;

            // We need at least one edge for things to work properly, so bootstrap with a trivial edge
            AddEdge(ExternalSource.Instance, ExternalEffect.Instance);

            // External effects, by definition, have depth zero.
            _assignedDepths[ExternalEffect.Instance] = 0;
        }

        private void CreateNode(object node)
        {
            if (!_fwdEdges.ContainsKey(node))
            {
                _fwdEdges[node] = new HashSet<object>();
            }

            if (!_revEdges.ContainsKey(node))
            {
                _revEdges[node] = new HashSet<object>();
            }

            _visitedDownstream[node] = 0;
            _unprocessedNodes.Add(node);
        }
        
        private void AddEdge(object from, object to)
        {
            CreateNode(from);
            CreateNode(to);

            _fwdEdges[from].Add(to);
            _revEdges[to].Add(from);

            Debug.Log($"[AlignNodes] Adding edge {from} -> {to}");
        }

        internal static Dictionary<object, EffectGroup> CreateEffectGroups(BakeContext context, ReactionGraph graph)
        {
            // TODO: group multiple effects that always activate together into the same condition nodes
            return graph.Nodes.SelectMany(n => n.Effects.Select(e => (n, e)))
                .GroupBy(pair => pair.e.TargetKey)
                .Select(g => new EffectGroup(context, g.Key, g.Select(p => p.n).ToList()))
                .ToDictionary(kv => kv.TargetKey, kv => kv);
        }

        public static List<EffectGroup> Apply(BakeContext context, ReactionGraph graph)
        {
            return Apply(context, CreateEffectGroups(context, graph));
        }

        private void AddInitialEdges(Dictionary<object, EffectGroup> byEffect)
        {
            object? downstream = null;

            foreach (var (k, v) in byEffect)
            {
                // v drives k
                if (k is InternalParameterTarget)
                {
                    AddEdge(v, k);
                    // Ensure that dead-end internal parameters are still connected to the graph and get assigned a depth
                    AddEdge(k, ExternalEffect.Instance);
                }

                // Add an outbound edge from each EffectGroup so that we never have unrooted nodes, even if there is
                // no associated effect.
                AddEdge(v, ExternalEffect.Instance);

                downstream = v;
                foreach (var node in v.Nodes)
                {
                    // We deep clone the expression here as we will be mutating it in-place when we insert delay nodes.
                    // We also need unique nodes for each EffectGroup that shares condition nodes, as we currently can't
                    // insert delay nodes between combinator nodes.
                    var expr = node.Expression.DeepClone();
                    ExprVisitor(ref expr);
                    node.Expression = expr;
                }

                void ExprVisitor(ref IExpression e)
                {
                    switch (e)
                    {
                        case InternalParameterCondition ipc:
                            AddEdge(new InternalParameterTarget(ipc.ParameterName), downstream);
                            return;
                        case ParameterExpression:
                            AddEdge(ExternalSource.Instance, downstream);
                            return;
                    }

                    var edgeKey = (v, e);

                    AddEdge(edgeKey, downstream);
                    var savedTarget = downstream;

                    downstream = edgeKey;
                    var edgeCount = _revEdges.GetValueOrDefault(edgeKey)?.Count ?? 0;
                    e.Walk(ExprVisitor);
                    var newEdgeCount = _revEdges.GetValueOrDefault(edgeKey)?.Count ?? 0;
                    if (newEdgeCount == edgeCount)
                    {
                        // Add fallback edge from ExternalSource if this expression doesn't reference any parameters
                        // or child expressions, to bootstrap the graph
                        AddEdge(ExternalSource.Instance, edgeKey);
                    }
                    downstream = savedTarget;
                }
            }
        }

        private void TopoAssignDepths()
        {
            var toVisit = new Queue<object>();
            toVisit.Enqueue(ExternalEffect.Instance);

            while (toVisit.Count > 0)
            {
                var next = toVisit.Dequeue();
                var nextDepth = _assignedDepths[next];

                var depth = nextDepth + NodeLatency(next);

                foreach (var prev in _revEdges.TryGetValue(next, out var rev) ? rev : Enumerable.Empty<object>())
                {
                    if (_assignedDepths.TryGetValue(prev, out var prevDepth))
                    {
                        prevDepth = Math.Max(prevDepth, depth);
                    }
                    else
                    {
                        prevDepth = depth;
                    }

                    _assignedDepths[prev] = prevDepth;
                    if (++_visitedDownstream[prev] == _fwdEdges[prev].Count)
                    {
                        toVisit.Enqueue(prev);
                    }

                    Debug.Log(
                        $"[AlignNodes] Visited {prev} -> {next}, depth {depth}, downstream {_visitedDownstream[prev]}/{_fwdEdges[prev].Count}");
                }
            }
        }

        private int NodeLatency(object next)
        {
            switch (next)
            {
                case EffectGroup eg:
                    return eg.Latency;
                default:
                    return 0;
            }
        }

        internal static List<EffectGroup> Apply(BakeContext context, Dictionary<object, EffectGroup> byEffect)
        {
            // Our high level algorithm is as follows: We will create a dataflow graph containing all externally
            // visible effects, condition nodes, and parameters. We will then perform a topological sort, assigning
            // depths to each node.
            //
            // We have the following vertices in the graph:
            // - (singleton) ExternalSource: A single source node representing all external inputs.
            // - (singleton) ExternalEffect: A single sink node representing all external effects, at depth zero.
            // - InternalParameter: A node for each internal parameter. Treated as having latency zero (latency is
            //   accounted for at the EffectGroup driving the InternalParameter
            // - ConditionNode
            // - EffectGroup
            // - Delayed parameters: A node for a delayed internal or external parameter. We'll add a simple direct
            // blend tree branch to forward the raw value of the parameter.
            //
            // We apply the following constraint to the graph: All inputs to a node have the same latency
            // We then inject delay nodes where needed to enforce this constraint.
            //
            // Note that currently, IR emission only supports delay nodes around InternalParameterCondition or
            // ParameterExpression (ie - not around intermediate And/Or nodes).

            var transform = new AlignNodesTransform(context);

            transform.AddInitialEdges(byEffect);
            transform.TopoAssignDepths();
            transform.CheckForCycles();
            transform.InjectDelayNodes(byEffect);

            return byEffect.Values.ToList();
        }

        private void InjectDelayNodes(Dictionary<object, EffectGroup> byEffect)
        {
            if (!_assignedDepths.TryGetValue(ExternalSource.Instance, out var externalSourceDepth))
            {
                externalSourceDepth = 0; // We should have only constant nodes, so this is a bit arbitrary
            }

            foreach (var (k, v) in byEffect)
            {
                var effectDepth = _assignedDepths[v];
                var expectedParamDepth = effectDepth + v.Latency;
                foreach (var node in v.Nodes)
                {
                    var expr = node.Expression;
                    WalkExpr(ref expr);
                    node.Expression = expr;
                }


                void WalkExpr(ref IExpression expression)
                {
                    switch (expression)
                    {
                        case InternalParameterCondition ip:
                        {
                            var name = ip.ParameterName;
                            var depth = _assignedDepths[new InternalParameterTarget(name)];
                            var delay = depth - expectedParamDepth;
                            if (delay > 0)
                            {
                                ip.ParameterName = GetDelayedParameter(ip.ParameterName, delay);
                            }

                            break;
                        }
                        case ParameterExpression pe:
                        {
                            if (IsDelayParam(pe.ParameterName))
                            {
                                throw new Exception("duplicate delay application");
                            }

                            var depth = _assignedDepths[ExternalSource.Instance];
                            var delay = depth - expectedParamDepth;
                            if (delay > 0)
                            {
                                pe.ParameterName = GetDelayedParameter(pe.ParameterName, delay);
                            }

                            break;
                        }
                        default:
                            expression.Walk(WalkExpr);
                            break;
                    }
                }
            }
        }

        internal static string DelayParamName(string baseName, int delay)
        {
            return $"$$MA/RC/DELAY/{baseName}/${delay}";
        }

        internal static bool IsDelayParam(string name)
        {
            return name.StartsWith("$$MA/RC/DELAY/");
        }

        internal static string DelayParamBaseName(string delayName)
        {
            var rest = delayName.Substring("$$MA/RC/DELAY/".Length);
            return rest.Substring(0, rest.LastIndexOf("/$"));
        }

        private string GetDelayedParameter(string ipParameterName, int delay)
        {
            if (delay == 0)
            {
                return ipParameterName;
            }

            for (var i = delay; i > 0; i--)
            {
                var delayedName = DelayParamName(ipParameterName, i);
                var prevDelay = i > 1 ? DelayParamName(ipParameterName, i - 1) : ipParameterName;

                if (!_createdDelayParameters.Add(delayedName))
                {
                    break;
                }

                context.SetParameter(delayedName, context.GetParameterInitialValue(ipParameterName));

                var linearMotionClip = VirtualClip.Create($"Delay {ipParameterName} {i}");
                linearMotionClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), delayedName),
                    AnimationCurve.Constant(0, 1, 1)
                );
                context.RootTree.Children = context.RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = linearMotionClip,
                    DirectBlendParameter = prevDelay
                });
            }

            return DelayParamName(ipParameterName, delay);
        }


        private void CheckForCycles()
        {
            var unprocessed = new HashSet<object>(
                _visitedDownstream.Keys.Where(n => _visitedDownstream[n] < _fwdEdges[n].Count)
            );

            if (unprocessed.Count == 0) return;

            var visited = new HashSet<object>();
            var path = new List<object>();
            var onPath = new HashSet<object>();

            foreach (var start in unprocessed)
            {
                if (!visited.Contains(start))
                    DfsFindCycle(start, unprocessed, visited, path, onPath);
            }

            // Should be unreachable: every unprocessed node must have an unprocessed downstream neighbor,
            // so the subgraph must contain at least one cycle. Dump the graph for diagnosis.
            var sb = new StringBuilder();
            sb.AppendLine($"Reaction graph has {unprocessed.Count} unprocessed nodes but no cycle was found.");
            sb.AppendLine("All edges (id:label):");
            foreach (var (from, targets) in _fwdEdges)
            {
                var fromId = RuntimeHelpers.GetHashCode(from);
                var fromMark = _assignedDepths.ContainsKey(from) ? "+" : "-";
                foreach (var to in targets)
                {
                    var toId = RuntimeHelpers.GetHashCode(to);
                    var toMark = _assignedDepths.ContainsKey(to) ? "+" : "-";
                    sb.AppendLine($"  [{fromMark}]{fromId:x8}:{from} -> [{toMark}]{toId:x8}:{to}");
                }
            }

            Debug.LogError(sb.ToString());
            throw new Exception(
                $"Reaction graph has {unprocessed.Count} nodes that could not be processed (cycle not found)");
        }

        private void DfsFindCycle(object node, HashSet<object> unprocessed, HashSet<object> visited,
            List<object> path, HashSet<object> onPath)
        {
            onPath.Add(node);
            path.Add(node);

            if (_fwdEdges.TryGetValue(node, out var fwd))
            {
                foreach (var next in fwd)
                {
                    if (!unprocessed.Contains(next)) continue;

                    if (onPath.Contains(next))
                    {
                        var cycleStartIdx = path.IndexOf(next);
                        var cycleNodes = path.Skip(cycleStartIdx).Select(n => n.ToString()).ToList();
                        cycleNodes.Add(next.ToString());
                        throw new Exception(
                            $"Cycle detected in reaction graph: {string.Join(" -> ", cycleNodes)}");
                    }

                    if (!visited.Contains(next))
                        DfsFindCycle(next, unprocessed, visited, path, onPath);
                }
            }

            path.RemoveAt(path.Count - 1);
            onPath.Remove(node);
            visited.Add(node);
        }

        private class ExternalSource
        {
            public static readonly ExternalSource Instance = new();

            public override string ToString()
            {
                return "ExternalSource";
            }
        }

        private class ExternalEffect
        {
            public static readonly ExternalEffect Instance = new();

            public override string ToString()
            {
                return "ExternalEffect";
            }
        }
    }
}