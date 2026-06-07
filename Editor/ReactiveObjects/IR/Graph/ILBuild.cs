#nullable enable

using System;
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
            AssertDecomposed(graph);
            ProcessExternalObjectStateInputsTransform.Apply(graph, context);
            AssertDecomposed(graph);
            // Eliminates redundant ObjectActiveState conditions, and forwards simple conditions
            // to downstream nodes
            ForwardObjectActiveDriversTransform.Apply(graph);
            AssertDecomposed(graph);
            BooleanSimplifyTransform.Apply(graph);
            AssertDecomposed(graph);

            ConvertToInternalParametersTransform.Apply(graph, context);

            // ConvertToInternalParameters introduces new effects on existing nodes, so we need to decompose again
            DecomposeTransform.Apply(graph);
            AssertDecomposed(graph);

            AssignInitialStates.ProcessGraph(context, graph);
            AssertDecomposed(graph);
            
            BreakLoopsTransform.Apply(graph);
            AssertDecomposed(graph);

            PruneUnusedInternalParametersTransform.Apply(graph);
            AssertDecomposed(graph);

            // Remove RC parameters whose nodes were pruned from the graph so they don't
            // remain in the animator with stale or incorrect default values.
            context.PruneOrphanedInternalParameters(graph);

            var subgraphs = SplitIntoSubgraphsTransform.Apply(graph);

            List<IMotionNode> motions = new();
            foreach (var subgraph in subgraphs)
            {
                var byEffect = AlignNodesTransform.CreateEffectGroups(context, subgraph);
                EffectGroupDumper.DumpEffectGroups(byEffect.Values, "Pre-align");
                var aligned = AlignNodesTransform.Apply(context, byEffect);
                AssignInitialStates.ProcessGroups(context, aligned);
                motions.AddRange(aligned.Select(g => g.Emit()));
            }

            return motions;
        }

        private static void AssertDecomposed(ReactionGraph graph)
        {
            foreach (var n in graph.Nodes)
            {
                if (n.Effects.Count != 1)
                {
                    throw new Exception("Expected node to be decomposed");
                }
            }
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
