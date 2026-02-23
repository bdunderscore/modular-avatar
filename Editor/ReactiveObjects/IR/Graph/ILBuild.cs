using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class ILBuild
    {
        private static List<IMotionNode> ApplyTransformations(BakeContext context, ReactionGraph graph)
        {
            DecomposeTransform.Apply(graph);
            ProcessExternalObjectStateInputsTransform.Apply(graph, context);
            // Eliminates redundant ObjectActiveState conditions, and forwards simple conditions
            // to downstream nodes
            ForwardObjectActiveDriversTransform.Apply(graph);
            BooleanSimplifyTransform.Apply(graph);

            AssignInitialStates.ProcessGraph(context, graph);

            ConvertToInternalParametersTransform.Apply(graph, context);
            BreakLoopsTransform.Apply(graph);
            var groups = SplitIntoSubgraphsTransform.Apply(graph);

            List<IMotionNode> motions = new();
            foreach (var group in groups)
            {
                var aligned = AlignNodesTransform.Apply(context, group);
                AssignInitialStates.ProcessGroups(context, aligned);
                motions.AddRange(aligned.Select(g => g.Emit()));
            }

            return motions;
        }

        private static void BuildMotions(BakeContext context, List<IMotionNode> nodes)
        {
            foreach (var node in nodes)
            {
                var motion = node.Bake(context);
                context.RootTree.Children = context.RootTree.Children.Add(
                    new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = BakeContext.ALWAYS_ONE,
                        Motion = motion
                    }
                );
            }
        }

        public static void Build(BakeContext context, ReactionGraph graph)
        {
            var motions = ApplyTransformations(context, graph);
            BuildMotions(context, motions);
        }
    }
}