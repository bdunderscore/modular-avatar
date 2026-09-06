using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal class OneDBlendNode : IMotionNode
    {
        public string Parameter { get; set; }

        public List<(float, IMotionNode)> Nodes { get; } = new();

        public OneDBlendNode(string parameter)
        {
            Parameter = parameter;
        }

        public VirtualMotion Bake(UnityBlendTreeBackend backend)
        {
            var empty = backend.EmptyMotion;

            var vbt = VirtualBlendTree.Create("OneDBlend " + Parameter);
            vbt.BlendType = BlendTreeType.Simple1D;
            vbt.BlendParameter = Parameter;
            vbt.UseAutomaticThresholds = false;
            vbt.NormalizedBlendValues = false;

            var builder = ImmutableList.CreateBuilder<VirtualBlendTree.VirtualChildMotion>();
            var pendingThreshold = float.NegativeInfinity;
            IMotionNode pendingNode = new EmptyNode();

            foreach (var node in Nodes)
            {
                if (node.Item1 < pendingThreshold)
                {
                    throw new InvalidOperationException("OneDBlendNode nodes must be in ascending order");
                }

                if (node.Item1 == pendingThreshold)
                {
                    // Replace the prior threshold since it's zero-width
                    pendingThreshold = node.Item1;
                    pendingNode = node.Item2;
                    continue;
                }

                // Emit the prior node in the range (pendingThreshold.NextLargest(), node.threshold)
                var priorNode = pendingNode.Bake(backend);
                if (builder.Count > 0)
                {
                    // Emit the lower bound
                    builder.Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Motion = priorNode,
                        Threshold = pendingThreshold.NextLargest()
                    });
                }

                builder.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = priorNode,
                    Threshold = node.Item1
                });
                pendingThreshold = node.Item1;
                pendingNode = node.Item2;
            }

            // Emit the final node
            if (float.IsNegativeInfinity(pendingThreshold))
            {
                return pendingNode.Bake(backend);
            }

            var finalNode = pendingNode.Bake(backend);
            builder.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = finalNode,
                Threshold = pendingThreshold.NextLargest()
            });
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