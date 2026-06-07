#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Detects and breaks cycles in the reaction graph by replacing InternalParameterDrivers with
    ///     normal ParameterDrivers on nodes that are part of cycles.
    ///     Cycles are formed when a node's expression depends on an InternalParameterCondition that is
    ///     driven by another node, creating a feedback loop.
    ///     When breaking loops, we prefer to break edges associated with actions on nodes that have a
    ///     higher priority number (nodes which are last in the list) first.
    /// </summary>
    public static class BreakLoopsTransform
    {
        // Step 8 of the transformation pipeline: detects and breaks cycles in the reaction graph
        public static void Apply(ReactionGraph graph)
        {
            if (graph.Nodes.Count == 0) return;

            // Find all internal parameters that are both driven and referenced in expressions
            var parametersToDrivenBy = BuildParameterDependencyMap(graph);

            // Detect which parameters are involved in cycles
            var cyclicParameters = DetectCyclicParameters(graph, parametersToDrivenBy);
            
            // Break only the selected parameters
            BreakCyclicParameters(graph, cyclicParameters);
        }

        /// <summary>
        ///     Maps each internal parameter name to all nodes that drive it.
        ///     If multiple nodes drive the same parameter, the last node (highest priority) wins,
        ///     but all driver nodes can participate in cycles.
        /// </summary>
        private static Dictionary<string, List<ReactionNode>> BuildParameterDependencyMap(ReactionGraph graph)
        {
            var map = new Dictionary<string, List<ReactionNode>>();

            foreach (var node in graph.Nodes)
            {
                foreach (var effect in node.Effects.OfType<DriveInternalParameter>())
                {
                    if (!map.ContainsKey(effect.ParameterName))
                    {
                        map[effect.ParameterName] = new List<ReactionNode>();
                    }

                    if (!map[effect.ParameterName].Contains(node))
                    {
                        map[effect.ParameterName].Add(node);
                    }
                }
            }

            return map;
        }

        /// <summary>
        ///     Detects which parameters are involved in cycles using DFS.
        ///     A parameter is cyclic if it participates in a dependency cycle.
        ///     The algorithm:
        ///     1. Do a DFS from each node (skipping any edges for parameters which are already marked as cyclic)
        ///     2. When a node reads a parameter, check if any driver of that parameter is in the current recursion path
        ///     3. If so, identify the edge associated with the driver node with the highest priority, and mark that
        ///        parameter as cyclic, then return back to that edge.
        /// </summary>
        private static HashSet<string> DetectCyclicParameters(
            ReactionGraph graph,
            Dictionary<string, List<ReactionNode>> parametersToDrivenBy)
        {
            var cyclicParameters = new HashSet<string>();
            var visitedGlobal = new HashSet<ReactionNode>();
            var parameterStack = new List<string>();
            var recursionSet = new Dictionary<string, ReactionNode>();

            foreach (var node in graph.Nodes)
            {
                string? removedParameter = null;
                if (!visitedGlobal.Contains(node))
                {
                    Visit(node, ref removedParameter);
                }
            }

            return cyclicParameters;

            void Visit(ReactionNode node, ref string? removedParameter)
            {
                if (node.Effects[0] is not DriveInternalParameter driveAction) return;
                var myParam = driveAction.ParameterName;

                if (recursionSet.ContainsKey(myParam))
                {
                    removedParameter = SelectParameterForRemoval(myParam);
                    cyclicParameters.Add(removedParameter);
                    return;
                }

                recursionSet[myParam] = node;
                parameterStack.Add(myParam);

                var incompleteSearch = false;
                foreach (var param in FindReferencedInternalParameters(node.Expression))
                {
                    if (cyclicParameters.Contains(param)) continue;
                    if (!parametersToDrivenBy.TryGetValue(param, out var drivers)) continue;

                    foreach (var driver in drivers)
                    {
                        if (visitedGlobal.Contains(driver)) continue;

                        Visit(driver, ref removedParameter);

                        if (removedParameter != null)
                        {
                            incompleteSearch = true;
                            break;
                        }
                    }

                    if (removedParameter == param)
                    {
                        removedParameter = null;
                    }
                    else if (removedParameter != null)
                    {
                        incompleteSearch = true;
                        break;
                    }
                }

                recursionSet.Remove(myParam);
                parameterStack.RemoveAt(parameterStack.Count - 1);

                if (!incompleteSearch)
                {
                    visitedGlobal.Add(node);
                }
            }

            string SelectParameterForRemoval(string param)
            {
                var firstIndex = parameterStack.IndexOf(param);

                var paramToBreak = parameterStack.Skip(firstIndex)
                    .SelectMany(cyclicParam =>
                        parametersToDrivenBy.GetValueOrDefault(cyclicParam)
                        ?? Enumerable.Empty<ReactionNode>())
                    .Select(node => (node.Priority, ((DriveInternalParameter)node.Effects[0]).ParameterName))
                    .OrderBy(node => node.Priority)
                    .Last()
                    .ParameterName;

                return paramToBreak;
            }   
        }

        /// <summary>
        ///     Finds all InternalParameterCondition references in an expression tree.
        /// </summary>
        private static HashSet<string> FindReferencedInternalParameters(IExpression expr)
        {
            var parameters = new HashSet<string>();

            void Visit(ref IExpression e)
            {
                if (e is InternalParameterCondition ipc)
                {
                    parameters.Add(ipc.ParameterName);
                }
                else
                {
                    e.Walk(Visit);
                }
            }

            var tmp = expr;
            Visit(ref tmp);
            return parameters;
        }

        /// <summary>
        ///     Breaks cycles by converting InternalParameterDrivers of selected cyclic parameters to DriveParameter.
        ///     For each selected parameter, breaks ALL drivers of that parameter and converts all references.
        ///     Prefers to break edges on nodes with higher priority numbers first.
        /// </summary>
        private static void BreakCyclicParameters(
            ReactionGraph graph,
            HashSet<string> parametersToBreak
        )
        {
            if (parametersToBreak.Count == 0) return;

            foreach (var node in graph.Nodes)
            {
                if (node.Effects[0] is DriveInternalParameter dip && parametersToBreak.Contains(dip.ParameterName))
                {
                    node.Effects[0] = new DriveParameter(dip.ParameterName, dip.State ? 1.0f : 0.0f);
                }

                var nodeExpression = node.Expression;
                WalkExpression(ref nodeExpression);
                node.Expression = nodeExpression;
            }

            void WalkExpression(ref IExpression e)
            {
                if (e is InternalParameterCondition ipc && parametersToBreak.Contains(ipc.ParameterName))
                {
                    e = new ParameterExpression(ipc.ParameterName, 0.5f);
                }
                else
                {
                    e.Walk(WalkExpression);
                }
            }
        }
    }
}