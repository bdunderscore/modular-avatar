using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Unity.Mathematics;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal class CoalesceBranchesTransform
    {
        private struct CandidateBranch
        {
            public float Start, End;
            public IMotionNode Inner;
        }

        public static void Apply(ref IMotionNode motionNode)
        {
            if (motionNode is not BranchNode bn)
            {
                motionNode.WalkTree(Apply);
                return;
            }

            var nodes = ImmutableList<(float, IMotionNode)>.Empty;
            var branches = CollectBranches(bn);
            
            var iterator = float.MinValue;
            IMotionNode? priorNode = null;
            foreach (var elem in branches.Values)
            {
                if (elem.Inner == null)
                {
                    throw new NullReferenceException("BranchNode cannot have null branches");
                }
                
                if (elem.Start > iterator)
                {
                    nodes = nodes.Add((iterator, new EmptyNode()));
                }

                var start = elem.Start;

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (elem.Start == iterator && nodes.LastOrDefault().Item2 == elem.Inner)
                {
                    // Two ranges that are adjacent with the same effect - merge them.
                    var removed = nodes.Last();
                    nodes = nodes.RemoveAt(nodes.Count - 1);
                    start = removed.Item1;
                }

                nodes = nodes.Add((start, elem.Inner));
                iterator = elem.End.NextLargest();
            }

            if (iterator < float.MaxValue)
            {
                nodes = nodes.Add((iterator, new EmptyNode()));
            }

            var oneD = new OneDBlendNode(bn.Parameter);
            oneD.Nodes = nodes;
            motionNode = oneD;
            motionNode.WalkTree(Apply);
        }

        private static SortedList<float, CandidateBranch> CollectBranches(
            BranchNode root
        )
        {
            var list = new SortedList<float, CandidateBranch>();
            Visit(root.OnLessEquals, float.NegativeInfinity, root.Threshold);
            if (!float.IsPositiveInfinity(root.Threshold))
            {
                Visit(root.OnGreaterThan, root.Threshold.NextLargest(), float.PositiveInfinity);
            }

            return list;

            void Visit(IMotionNode branch, float min, float max)
            {
                if (min > max) return; // empty interval (impossible branch)

                if (branch is ProxyNode pn && pn.Target != null)
                {
                    Visit(pn.Target, min, max);
                    return;
                }
                
                if (branch is not BranchNode bn || bn.Parameter != root.Parameter)
                {
                    list.Add(min, new CandidateBranch { Start = min, End = max, Inner = branch });
                    return;
                }

                Visit(bn.OnLessEquals, min, math.min(max, bn.Threshold));
                if (!float.IsPositiveInfinity(bn.Threshold))
                {
                    Visit(bn.OnGreaterThan, math.max(min, bn.Threshold.NextLargest()), max);
                }
            }
        }
    }
}