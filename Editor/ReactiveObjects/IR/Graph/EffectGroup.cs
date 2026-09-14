#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     An effect group represents a grouping of effects of which only one is actually applied (specifically, the last
    ///     one which is active in the list of ReactionNodes).
    /// </summary>
    internal class EffectGroup
    {
        public static EffectGroup Merge(List<EffectGroup> groups)
        {
            var firstGroup = groups[0];
            var nodeCount = groups[0].Nodes.Count;
            var newNodes = new List<ReactionNode>(nodeCount);

            if (groups.Any(g => firstGroup.Processor != g.Processor))
            {
                throw new ArgumentException("Cannot merge EffectGroups with different processors");
            }

            if (groups.Any(g => g.Latency != firstGroup.Latency))
            {
                throw new ArgumentException("Cannot merge EffectGroups with different latencies");
            }

            for (var i = 0; i < nodeCount; i++)
            {
                var expression = groups[0].Nodes[i].Expression;
                var node = new ReactionNode(expression);
                newNodes.Add(node);

                foreach (var group in groups)
                {
                    if (!group.Nodes[i].Expression.Equals(expression))
                    {
                        throw new InvalidOperationException(
                            $"Cannot merge EffectGroups with different expressions at index {i}: {expression} vs {group.Nodes[i].Expression}");
                    }

                    node.Effects.AddRange(group.Nodes[i].Effects);
                }
            }

            return new EffectGroup(firstGroup.Processor, firstGroup.TargetKey, newNodes, firstGroup.Latency);
        }

        public EffectGroup(IEffectProcessor processor, object targetKey, List<ReactionNode> nodes, int latency)
        {
            TargetKey = targetKey;
            Nodes = nodes;
            Latency = latency;
            Processor = processor;
        }

        public IMotionNode? Emit(UnityBlendTreeBackend backend)
        {
            return Processor.ProcessEffectGroup(backend, this);
        }

        public readonly object TargetKey;
        public readonly List<ReactionNode> Nodes;
        private readonly List<ProxyCondition> _proxyConditions = new();
        public readonly IEffectProcessor Processor;

        /// <summary>
        ///     The number of frames between the inputs to this node, to the outputs of the node.
        /// </summary>
        public readonly int Latency;

        /// <summary>
        ///     Represents the number of frames away this node is from an externally-visible effect.
        ///     It follows that external effects always have depth zero.
        /// </summary>
        public int? Depth;

        public int? DefaultNode;

        public override string ToString()
        {
            return $"EffectGroup({TargetKey})";
        }
    }
}
