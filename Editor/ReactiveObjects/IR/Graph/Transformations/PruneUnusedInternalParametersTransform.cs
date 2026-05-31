using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Removes EffectGroups (reaction nodes) that drive internal parameters which are not transitively
    ///     reachable from any node with an externally-visible (non-InternalParameter) target.
    /// </summary>
    public static class PruneUnusedInternalParametersTransform
    {
        public static void Apply(ReactionGraph graph)
        {
            // Build map: internal param name → nodes that drive it
            var paramToDriverNodes = new Dictionary<string, List<ReactionNode>>();
            foreach (var node in graph.Nodes)
            {
                if (node.Effects[0].TargetKey is InternalParameterTarget ipt)
                {
                    if (!paramToDriverNodes.TryGetValue(ipt.ParameterName, out var list))
                    {
                        list = new List<ReactionNode>();
                        paramToDriverNodes[ipt.ParameterName] = list;
                    }

                    list.Add(node);
                }
            }

            var usedParams = new HashSet<string>();
            var worklist = new Queue<string>();

            void MarkUsed(string paramName)
            {
                if (usedParams.Add(paramName))
                    worklist.Enqueue(paramName);
            }

            // Seed: collect internal params referenced by externally-visible nodes
            foreach (var node in graph.Nodes)
            {
                if (!(node.Effects[0].TargetKey is InternalParameterTarget))
                {
                    CollectInternalParams(node.Expression, MarkUsed);
                }
            }

            // Transitively follow: if param P is used, nodes that drive P may reference other params
            while (worklist.Count > 0)
            {
                var paramName = worklist.Dequeue();
                if (!paramToDriverNodes.TryGetValue(paramName, out var drivers)) continue;

                foreach (var driverNode in drivers)
                    CollectInternalParams(driverNode.Expression, MarkUsed);
            }

            // Remove nodes whose sole effect drives an unused internal parameter
            graph.Nodes.RemoveAll(node =>
                node.Effects[0].TargetKey is InternalParameterTarget ipt &&
                !usedParams.Contains(ipt.ParameterName));
        }

        private static void CollectInternalParams(IExpression expr, Action<string> markUsed)
        {
            switch (expr)
            {
                case InternalParameterCondition ipc:
                    markUsed(ipc.ParameterName);
                    break;
                case AndNode and:
                    foreach (var child in and.Children) CollectInternalParams(child, markUsed);
                    break;
                case OrNode or:
                    foreach (var child in or.Children) CollectInternalParams(child, markUsed);
                    break;
                case NotNode not:
                    CollectInternalParams(not.Inner, markUsed);
                    break;
            }
        }
    }
}