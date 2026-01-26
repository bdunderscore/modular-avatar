#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Groups reaction nodes into reaction groups (subgraphs). Two nodes are in the same group if one of the following is
    ///     true:
    ///     1. They contain the same (non-constant) ObjectActiveState target
    ///     2. They contain a ParameterCondition for the same parameter name
    ///     3. They have the same target for their action
    /// </summary>
    public static class SplitIntoSubgraphsTransform
    {
        public static List<ReactionGraph> Apply(ReactionGraph graph)
        {
            var nodeCount = graph.Nodes.Count;
            if (nodeCount == 0) return new List<ReactionGraph>();

            // Collect overlap keys per node
            var objectTargets = new Dictionary<int, HashSet<GameObject>>();
            var parameterNames = new Dictionary<int, HashSet<string>>();

            for (var i = 0; i < nodeCount; i++)
            {
                objectTargets[i] = new HashSet<GameObject>();
                parameterNames[i] = new HashSet<string>();
                CollectKeys(graph.Nodes[i].Expression, objectTargets[i], parameterNames[i]);
            }

            // Build key -> node index maps for quick adjacency construction (LINQ)
            var objectToNodes = objectTargets
                .SelectMany(kv => kv.Value.Select(obj => (obj, kv.Key)))
                .GroupBy(x => x.obj, x => x.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            var paramToNodes = parameterNames
                .SelectMany(kv => kv.Value.Select(param => (param, kv.Key)))
                .GroupBy(x => x.param, x => x.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            var actionToNodes = graph.Nodes
                .SelectMany((n, idx) => n.Effects.Select(effect => (idx, effect.TargetKey)))
                .GroupBy(x => x.TargetKey, x => x.idx)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Build disjoint sets connecting nodes sharing any key
            var parent = new int[nodeCount];
            var rank = new int[nodeCount];
            for (var i = 0; i < nodeCount; i++) parent[i] = i;

            int Find(int x)
            {
                if (parent[x] != x) parent[x] = Find(parent[x]);
                return parent[x];
            }

            void Union(int a, int b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb])
                {
                    parent[ra] = rb;
                }
                else if (rank[ra] > rank[rb])
                {
                    parent[rb] = ra;
                }
                else
                {
                    parent[rb] = ra;
                    rank[ra]++;
                }
            }

            void UnionAll(List<int> nodeList)
            {
                if (nodeList.Count <= 1) return;
                var first = nodeList[0];
                for (var j = 1; j < nodeList.Count; j++) Union(first, nodeList[j]);
            }

            foreach (var list in objectToNodes.Values) UnionAll(list);
            foreach (var list in paramToNodes.Values) UnionAll(list);
            foreach (var list in actionToNodes.Values) UnionAll(list);

            // Collect components
            var components = new Dictionary<int, List<int>>();
            for (var i = 0; i < nodeCount; i++)
            {
                var root = Find(i);
                if (!components.TryGetValue(root, out var list))
                {
                    list = new List<int>();
                    components[root] = list;
                }

                list.Add(i);
            }

            var subgraphs = new List<ReactionGraph>();
            foreach (var comp in components.Values)
            {
                var subgraph = new ReactionGraph
                {
                    Nodes = comp
                        .Select(idx => graph.Nodes[idx])
                        .OrderBy(n => n.Priority)
                        .ToList()
                };
                subgraphs.Add(subgraph);
            }

            subgraphs = subgraphs
                .OrderBy(g => g.Nodes.Count == 0 ? int.MaxValue : g.Nodes.Min(n => n.Priority))
                .ToList();

            return subgraphs;
        }

        private static void CollectKeys(IExpression expr, HashSet<GameObject> objects, HashSet<string> parameters)
        {
            switch (expr)
            {
                case ObjectActiveState oas:
                    if (oas.TargetObject != null) objects.Add(oas.TargetObject);
                    break;
                case ParameterExpression pe:
                    if (!string.IsNullOrEmpty(pe.ParameterName)) parameters.Add(pe.ParameterName);
                    break;
                case InternalParameterCondition ipc:
                    if (!string.IsNullOrEmpty(ipc.ParameterName)) parameters.Add(ipc.ParameterName);
                    break;
                case AndNode and:
                    foreach (var child in and.Children) CollectKeys(child, objects, parameters);
                    break;
                case OrNode or:
                    foreach (var child in or.Children) CollectKeys(child, objects, parameters);
                    break;
                case NotNode not:
                    CollectKeys(not.Inner, objects, parameters);
                    break;
            }
        }
    }
}