using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal class OneDBlendNode : IMotionNode
    {
        public string Parameter { get; set; }

        // Each pair has the _first_ value that is mapped to this node
        public List<(float, IMotionNode)> Nodes { get; } = new();

        public OneDBlendNode(string parameter)
        {
            Parameter = parameter;
        }

        public VirtualMotion Bake(UnityBlendTreeBackend backend)
        {
            var empty = backend.EmptyMotion;

            var nodesWithLowerSentinel = new (float, VirtualMotion)[] { (float.NegativeInfinity, empty) }
                .Concat(Nodes.Select(n => (n.Item1, n.Item2.Bake(backend))))
                .Select(pair => (threshold: pair.Item1, node: pair.Item2))
                .ToList();

            var finalNode = nodesWithLowerSentinel[^1];

            // Drop all zero-width intervals, retaining the final real node explicitly.
            var adjustedNodes = nodesWithLowerSentinel
                .Zip(nodesWithLowerSentinel.Skip(1), (a, b) => (first: a, next: b.Item1))
                .Where(pair => pair.first.threshold < pair.next)
                .Select(pair => pair.first)
                .Append(finalNode)
                .ToList();

            if (adjustedNodes.Count == 1)
            {
                return adjustedNodes[0].node;
            }

            var vbt = VirtualBlendTree.Create("OneDBlend " + Parameter);
            vbt.BlendType = BlendTreeType.Simple1D;
            vbt.BlendParameter = Parameter;
            vbt.UseAutomaticThresholds = false;
            vbt.NormalizedBlendValues = false;

            var builder = ImmutableList.CreateBuilder<VirtualBlendTree.VirtualChildMotion>();

            // At least two distinct ranges remain, so emit each boundary without blending.
            IEnumerable<((float threshold, VirtualMotion motion) start, (float threshold, VirtualMotion motion) end)>
                intervals = adjustedNodes.Zip(adjustedNodes.Skip(1), (a, b) => (start: a, end: b));

            foreach (var (start, end) in intervals)
            {
                builder.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Threshold = end.threshold.NextSmallest(),
                    Motion = start.motion
                });
                builder.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Threshold = end.threshold,
                    Motion = end.motion
                });
            }

            vbt.Children = builder.ToImmutable();
            return vbt;
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
            for (var i = 0; i < Nodes.Count; i++)
            {
                var tuple = Nodes[i];
                visitor(ref tuple.Item2);
                Nodes[i] = tuple;
            }
        }
    }
}