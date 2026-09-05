#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.StaticProcessing
{
    internal static class StaticProcessing
    {
        internal static void Apply(ReactionGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            RemoveRulesBeforeLastConstantTrue(graph);
            graph.Nodes.RemoveAll(node => node.Effects.Count == 0);
            var finalEffects = FindFinalEffects(graph);

            using (var context = new StaticApplyContext())
            {
                foreach (var node in graph.Nodes.ToArray())
                {
                    if (node.Expression is not Constant { Value: true } ||
                        !node.Effects.All(effect =>
                            finalEffects.TryGetValue(effect.TargetKey, out var final) &&
                            ReferenceEquals(final.Node, node) &&
                            ReferenceEquals(final.Action, effect)))
                        continue;

                    for (var index = 0; index < node.Effects.Count; index++)
                    {
                        var action = node.Effects[index];
                        if (action is AlreadyApplied) continue;

                        if (action.ApplyStatic(context) == StaticApplyResult.Applied)
                            node.Effects[index] = new AlreadyApplied(action);
                    }
                }
            }

            RemoveAppliedMeshHides.Apply(graph);
        }

        private static Dictionary<object, (ReactionNode Node, IAction Action)> FindFinalEffects(ReactionGraph graph)
        {
            var finalEffects = new Dictionary<object, (ReactionNode Node, IAction Action)>();
            foreach (var node in graph.Nodes)
            {
                foreach (var effect in node.Effects)
                    finalEffects[effect.TargetKey] = (node, effect);
            }

            return finalEffects;
        }

        private static void RemoveRulesBeforeLastConstantTrue(ReactionGraph graph)
        {
            var lastConstants = new Dictionary<object, int>();
            for (var nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
            {
                var node = graph.Nodes[nodeIndex];
                if (node.Expression is not Constant { Value: true }) continue;

                foreach (var effect in node.Effects)
                    lastConstants[effect.TargetKey] = nodeIndex;
            }

            for (var nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
            {
                var currentIndex = nodeIndex;
                graph.Nodes[nodeIndex].Effects.RemoveAll(effect =>
                    lastConstants.TryGetValue(effect.TargetKey, out var lastConstantIndex) &&
                    currentIndex < lastConstantIndex);
            }
        }
    }
}