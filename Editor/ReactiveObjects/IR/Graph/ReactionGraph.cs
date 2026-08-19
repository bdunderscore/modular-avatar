#nullable enable

using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    public class ReactionGraph
    {
        public List<ReactionNode> Nodes = new();

        internal void AddNode(ReactionNode node)
        {
            Nodes.Add(node);
        }
    }
}
