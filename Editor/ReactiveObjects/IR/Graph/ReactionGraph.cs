#nullable enable

using System;
using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal class ReactionGraph
    {
        public List<ReactionNode> Nodes = new();

        internal ReactionParameters Parameters { get; }

        public ReactionGraph() : this(new ReactionParameters())
        {
        }

        internal ReactionGraph(ReactionParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        internal void AddNode(ReactionNode node)
        {
            Nodes.Add(node);
        }
    }
}
