using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     The priority node selects the first of several conditions that are true.
    /// </summary>
    public class PriorityNode : IConditionNode
    {
        public IConditionNode DefaultCondition = EmptyNode.Instance;
        public List<(ProxyCondition, IConditionNode)> Conditions { get; set; } = new();

        // 1 frame for the summation
        public int MaxLatency => 1;

        public VirtualMotion Bake(BakeContext context)
        {
            // Because we need a buffer zone between entries, we can't use every bit. In particular, we want to ensure
            // that we leave two floats between ranges. This means we need to avoid using the low two bits. As such,
            // we can encode up to 20 conditions per node.

            var tmpConditions = Conditions.ToList();
            VirtualMotion? subExecution = null, subSum = null;
            if (Conditions.Count > 20)
            {
                // Extract the remaining conditions to a sub-node. To keep the execution latency down, we'll evaluate
                // its conditions at the same time as we do our own.
                var subNode = new PriorityNode();
                subNode.DefaultCondition = DefaultCondition;
                foreach (var cond in Conditions.Skip(19))
                {
                    subNode.Conditions.Add(cond);
                }

                Conditions.RemoveRange(19, Conditions.Count - 19);

                (subExecution, subSum) = subNode.ConstructTrees(context);

                DefaultCondition = new MotionNode(subExecution);
            }

            var (executionTree, sumTree) = ConstructTrees(context);

            if (subSum != null)
            {
                sumTree.Children = sumTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    DirectBlendParameter = BakeContext.ALWAYS_ONE,
                    Motion = subSum
                });
            }

            sumTree.Children = sumTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                DirectBlendParameter = BakeContext.ALWAYS_ONE,
                Motion = executionTree
            });

            return sumTree;
        }

        private (VirtualBlendTree executionTree, VirtualBlendTree sumTree) ConstructTrees(BakeContext context)
        {
            // We make use of the representation of floats here to implement our first-selected logic. Specifically,
            // IEEE floats store their fraction in the low 22 bits of their representation, with the leading bit removed.
            // We therefore assign each condition a power of two: Starting from a base value of 1, we add 2^-1, 2^-2, etc.
            // We then set a range on a 1D blend tree to capture the range up to the next threshold.

            // Assign a suitable range to each condition
            List<(float, float, float)> ranges = new();
            float curDelta = 1;
            float? initialState = null;
            for (var i = 0; i < Conditions.Count; i++)
            {
                curDelta /= 2;

                var myStart = 1 + curDelta;
                var upperStart = 1 + curDelta * 2;

                // We want to capture the range starting just before myStart, and ending two steps before upperStart
                myStart = myStart.NextSmallest();
                var myEnd = upperStart.NextSmallest().NextSmallest();

                ranges.Add((myStart, myEnd, curDelta));

                if (Conditions[i].Item1.InitialState && initialState == null)
                {
                    initialState = myStart;
                }
            }

            Debug.Log("[PriorityNode] Initial state = " + initialState);

            // We now construct the functional blend tree. This has two components: A direct blend tree, which
            // sums the conditions, and which then references a 1D blend tree which selects between them.
            var sumParam = context.AddParameter("PriorityNode", initialState ?? 1f);
            var sumTree = VirtualBlendTree.Create("PriorityNode sum");
            sumTree.BlendType = BlendTreeType.Direct;
            sumTree.NormalizedBlendValues = false;

            // Add base curve
            var baseMotion = VirtualClip.Create("PriorityNode base");
            baseMotion.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), sumParam),
                AnimationCurve.Constant(0, 1, 1f)
            );
            sumTree.Children = sumTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                DirectBlendParameter = BakeContext.ALWAYS_ONE,
                Motion = baseMotion
            });

            for (var i = 0; i < Conditions.Count; i++)
            {
                var (_, _, rangeDelta) = ranges[i];

                var sumCondition = VirtualClip.Create(sumParam + " index " + i);
                sumCondition.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), sumParam),
                    AnimationCurve.Constant(0, 1, rangeDelta)
                );

                var motion = Conditions[i].Item1.Flatten(EmptyNode.Instance, new MotionNode(sumCondition))
                    .Bake(context);
                sumTree.Children = sumTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    // TODO: Can we optimize this when our parameter is a bool to avoid the extra blendtree layer?
                    DirectBlendParameter = BakeContext.ALWAYS_ONE,
                    Motion = motion
                });
            }

            using var _scope = context.LatencyScope(1);

            // Now construct the execution blendtree
            var executionTree = VirtualBlendTree.Create("Execution");
            executionTree.BlendType = BlendTreeType.Simple1D;
            executionTree.BlendParameter = sumParam;
            executionTree.UseAutomaticThresholds = false;

            // If nothing is selected, we pull in the default condition
            var defaultMotion = DefaultCondition.Bake(context);
            executionTree.Children = executionTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Threshold = 0,
                Motion = defaultMotion
            }).Add(new VirtualBlendTree.VirtualChildMotion
            {
                Threshold = 1f.NextLargest(),
                Motion = defaultMotion
            });

            for (var i = 0; i < Conditions.Count; i++)
            {
                var (rangeStart, rangeEnd, _) = ranges[i];

                var conditionMotion = Conditions[i].Item2.Bake(context);

                executionTree.Children = executionTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Threshold = rangeStart,
                    Motion = conditionMotion
                });
                executionTree.Children = executionTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Threshold = rangeEnd,
                    Motion = conditionMotion
                });
            }

            executionTree.Children = executionTree.Children.OrderBy(x => x.Threshold).ToImmutableList();

            return (executionTree, sumTree);
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
            foreach (var cond in Conditions)
            {
                cond.Item1.Node.WalkTree(visitor);
            }
        }
    }
}