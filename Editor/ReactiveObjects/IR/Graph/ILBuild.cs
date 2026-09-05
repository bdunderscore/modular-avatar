#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class ILBuild
    {
        internal static void Simplify(ReactionGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            DecomposeTransform.Apply(graph);
            AssertDecomposed(graph);

            // Eliminates redundant ObjectActiveState conditions, and forwards simple conditions
            // to downstream nodes.
            ForwardObjectActiveDriversTransform.Apply(graph);
            BooleanSimplifyTransform.Apply(graph);
            RemoveConstantFalseNodesTransform.Apply(graph);
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
