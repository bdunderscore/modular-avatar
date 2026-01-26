#nullable enable

using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Breaks down all multi-effect nodes into single-effect nodes
    /// </summary>
    public static class DecomposeTransform
    {
        public static void Apply(ReactionGraph graph)
        {
            graph.Nodes = graph.Nodes
                .SelectMany(n => n.Effects.Select(effect => new ReactionNode(n.Expression, effect)))
                .ToList();

            // Reassign priorities after decomposition
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                graph.Nodes[i].Priority = i;
            }
        }
    }
}