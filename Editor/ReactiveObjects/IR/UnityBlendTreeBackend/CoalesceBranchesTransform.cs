using System.Collections.Generic;
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

            var oneD = new OneDBlendNode(bn.Parameter);
            var branches = CollectBranches(bn);

            var iterator = float.MinValue;
            IMotionNode? priorNode = null;
            foreach (var elem in branches.Values)
            {
                if (elem.Start > iterator)
                {
                    oneD.Nodes.Add((iterator, new EmptyNode()));
                }

                var start = elem.Start;

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (elem.Start == iterator && oneD.Nodes.LastOrDefault().Item2 == elem.Inner)
                {
                    // Two ranges that are adjacent with the same effect - merge them.
                    var removed = oneD.Nodes.Last();
                    oneD.Nodes.RemoveAt(oneD.Nodes.Count - 1);
                    start = removed.Item1;
                }

                oneD.Nodes.Add((start, elem.Inner));
                iterator = elem.End.NextLargest();
            }

            if (iterator < float.MaxValue)
            {
                oneD.Nodes.Add((iterator, new EmptyNode()));
            }

            motionNode = oneD;
            motionNode.WalkTree(Apply);
        }

        private static SortedList<float, CandidateBranch> CollectBranches(
            BranchNode root
        )
        {
            var list = new SortedList<float, CandidateBranch>();
            Visit(root.OnLessEquals, float.NegativeInfinity, root.Threshold);
            Visit(root.OnGreaterThan, root.Threshold.NextLargest(), float.PositiveInfinity);

            return list;

            void Visit(IMotionNode branch, float min, float max)
            {
                if (min >= max) return; // empty interval (impossible branch)

                if (branch is not BranchNode bn || bn.Parameter != root.Parameter)
                {
                    list.Add(min, new CandidateBranch { Start = min, End = max, Inner = branch });
                    return;
                }

                Visit(bn.OnLessEquals, min, math.min(max, bn.Threshold));
                Visit(bn.OnGreaterThan, math.max(min, bn.Threshold.NextLargest()), max);
            }
        }
    }
}