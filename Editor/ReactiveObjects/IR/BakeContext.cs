using System;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public sealed class BakeContext
    {
        public const string ALWAYS_ONE = "$$MA/RC/AlwaysOne";
        public AnimationIndex AnimationIndex { get; private set; }
        public ObjectPathRemapper ObjectPathRemapper { get; private set; }
        public CloneContext CloneContext { get; private set; }
        public VirtualMotion EmptyMotion { get; private set; }
        public VirtualClip BaseClip { get; }
        public VirtualBlendTree RootTree { get; }
        private readonly VirtualAnimatorController vac;
        private int counter;

        public int Latency { get; private set; }
        public int LatencyHorizon { get; private set; }

        public BakeContext(AnimatorServicesContext asc, VirtualAnimatorController vac)
        {
            AnimationIndex = asc.AnimationIndex;
            ObjectPathRemapper = asc.ObjectPathRemapper;
            CloneContext = asc.ControllerContext.CloneContext;
            
            EmptyMotion = VirtualClip.Create("Empty");
            this.vac = vac;

            BaseClip = VirtualClip.Create("Base");

            vac.Parameters = vac.Parameters.Add(ALWAYS_ONE, new AnimatorControllerParameter
            {
                name = ALWAYS_ONE,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1
            });

            RootTree = VirtualBlendTree.Create("Root");
            RootTree.BlendType = BlendTreeType.Direct;
            RootTree.NormalizedBlendValues = false;
            RootTree.UseAutomaticThresholds = false;

            RootTree.Children = RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = BaseClip,
                DirectBlendParameter = ALWAYS_ONE
            });
        }

        private void SetLatencyHorizon(IMotionNode root)
        {
            var highWaterMark = 0;
            var latency = 0;

            Walk(ref root);

            LatencyHorizon = highWaterMark;

            void Walk(ref IMotionNode node)
            {
                latency += node.Latency;
                highWaterMark = Math.Max(highWaterMark, latency);
                node.WalkTree(Walk);
                latency -= node.Latency;
            }
        }

        public void Bake(IMotionNode root)
        {
            SetLatencyHorizon(root);
            var rootMotion = root.Bake(this);

            RootTree.Children = RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = rootMotion,
                DirectBlendParameter = ALWAYS_ONE
            });
        }
        
        public string AddParameter(string prefix, float value)
        {
            var template = new AnimatorControllerParameter
            {
                name = "$$MA/RC/" + prefix + "$" + counter++,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = value
            };

            vac.Parameters = vac.Parameters.Add(template.name, template);

            return template.name;
        }

        public IDisposable LatencyScope(int frames)
        {
            var scope = new LatencyDisposable(this);
            Latency += frames;
            return scope;
        }

        private class LatencyDisposable : IDisposable
        {
            private readonly BakeContext context;
            private readonly int OriginalLatency;

            public LatencyDisposable(BakeContext context)
            {
                this.context = context;
                OriginalLatency = context.Latency;
            }

            public void Dispose()
            {
                context.Latency = OriginalLatency;
            }
        }
    }
}