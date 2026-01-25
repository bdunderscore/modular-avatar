using System;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public class LatencyHorizon : IMotionNode
    {
        private IMotionNode _downstream;

        public IMotionNode Downstream
        {
            get => _downstream;
            set => _downstream = value;
        }

        public LatencyHorizon(IMotionNode downstream)
        {
            Downstream = downstream;
        }

        public VirtualMotion Bake(BakeContext context)
        {
            if (context.Latency == context.LatencyHorizon)
            {
                return Downstream.Bake(context);
            }

            if (context.Latency > context.LatencyHorizon)
            {
                throw new Exception("Latency has exceeded the latency horizon");
            }

            // Introduce a number of delay steps here
            var nSteps = context.LatencyHorizon - context.Latency;
            var lastStep = nSteps - 1;
            var initial = VirtualClip.Create("Delay 0");
            var prior = initial;
            for (var i = 0; i < context.LatencyHorizon - context.Latency; i++)
            {
                var delayVariable = context.AddParameter("$$MA/RC/Delay/" + i, 0);
                prior.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), delayVariable),
                    AnimationCurve.Constant(0, 1, 1)
                );
                context.BaseClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), delayVariable),
                    AnimationCurve.Constant(0, 1, 0)
                );

                VirtualMotion current;
                if (i == lastStep)
                {
                    current = prior = VirtualClip.Create("Delay " + (i + 1));
                }
                else
                {
                    current = Downstream.Bake(context);
                }

                context.RootTree.Children = context.RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = current,
                    DirectBlendParameter = delayVariable
                });
            }

            return initial;
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
            visitor(ref _downstream);
        }
    }
}