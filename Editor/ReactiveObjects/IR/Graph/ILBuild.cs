#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class ILBuild
    {
        internal static IReadOnlyList<ReactionGraph> Optimize(IReactionBackend backend, ReactionGraph graph)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            DecomposeTransform.Apply(graph);
            AssertDecomposed(graph);

            // Eliminates redundant ObjectActiveState conditions, and forwards simple conditions
            // to downstream nodes.
            ForwardObjectActiveDriversTransform.Apply(graph);
            AssertDecomposed(graph);

            BooleanSimplifyTransform.Apply(graph);
            ConvertToInternalParametersTransform.Apply(backend, graph);
            BooleanSimplifyTransform.Apply(graph);

            // ConvertToInternalParameters introduces new effects on existing nodes, so we need to decompose again.
            DecomposeTransform.Apply(graph);
            AssertDecomposed(graph);

            AssignInitialStates.ProcessGraph(backend, graph);
            AssertDecomposed(graph);

            BreakLoopsTransform.Apply(graph);
            AssertDecomposed(graph);

            PruneUnusedInternalParametersTransform.Apply(graph);
            AssertDecomposed(graph);

            // Remove RC parameters whose nodes were pruned from the graph so they don't
            // remain in the animator with stale or incorrect default values.
            graph.Parameters.PruneOrphanedInternalParameters(graph);

            return SplitIntoSubgraphsTransform.Apply(graph);
        }

        private static void AssertDecomposed(ReactionGraph graph)
        {
            foreach (var node in graph.Nodes)
            {
                if (node.Effects.Count != 1)
                {
                    throw new Exception("Expected node to be decomposed");
                }
            }
        }
    }
}
