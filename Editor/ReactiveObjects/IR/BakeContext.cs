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
        public VirtualLayer BaseLayer { get; }
        public VirtualClip BaseLayerClip { get; }
        private readonly VirtualAnimatorController _vac;
        private int _counter;

        public int Latency { get; private set; }
        public int LatencyHorizon { get; private set; }

        public BakeContext(AnimatorServicesContext asc, VirtualAnimatorController vac)
        {
            AnimationIndex = asc.AnimationIndex;
            ObjectPathRemapper = asc.ObjectPathRemapper;
            CloneContext = asc.ControllerContext.CloneContext;
            
            EmptyMotion = VirtualClip.Create("Empty");
            _vac = vac;

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

            // Base layer at lowest priority to hold initial active-state defaults
            BaseLayerClip = VirtualClip.Create("BaseLayer");
            var baseBlendTree = VirtualBlendTree.Create("BaseLayerTree");
            baseBlendTree.BlendType = BlendTreeType.Direct;
            baseBlendTree.NormalizedBlendValues = false;
            baseBlendTree.UseAutomaticThresholds = false;
            baseBlendTree.Children = baseBlendTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = BaseLayerClip,
                DirectBlendParameter = ALWAYS_ONE
            });

            BaseLayer = vac.AddLayer(new LayerPriority(int.MinValue), "MA/RC Base");
            BaseLayer.BlendingMode = AnimatorLayerBlendingMode.Override;
            BaseLayer.DefaultWeight = 1;
            var sm = BaseLayer.StateMachine!;
            var state = sm.AddState("Base");
            sm.DefaultState = state;
            state.Motion = baseBlendTree;
        }

        private void SetLatencyHorizon(IMotionNode root)
        {
            var highWaterMark = 0;
            var latency = 0;

            void Walk(ref IMotionNode node)
            {
                latency += node.Latency;
                highWaterMark = Math.Max(highWaterMark, latency);
                node.WalkTree(Walk);
                latency -= node.Latency;
            }

            Walk(ref root);
            LatencyHorizon = highWaterMark;
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
                name = "$$MA/RC/" + prefix + "$" + _counter++,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = value
            };

            _vac.Parameters = _vac.Parameters.Add(template.name, template);

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
            private readonly BakeContext _context;
            private readonly int _originalLatency;

            public LatencyDisposable(BakeContext context)
            {
                _context = context;
                _originalLatency = context.Latency;
            }

            public void Dispose()
            {
                _context.Latency = _originalLatency;
            }
        }
    }
}