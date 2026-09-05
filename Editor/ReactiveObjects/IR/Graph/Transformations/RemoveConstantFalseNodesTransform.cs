#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    internal static class RemoveConstantFalseNodesTransform
    {
        public static void Apply(ReactionGraph graph)
        {
            graph.Nodes.RemoveAll(node => node.Expression is Constant { Value: false });
        }
    }
}