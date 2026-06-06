#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public sealed class BakeContext
    {
        public const string ALWAYS_ONE = "$$MA/RC/AlwaysOne";
        internal const string BASE_LAYER_NAME = "MA/RC Base";
        internal const string APPLY_LAYER_NAME = "MA/RC Apply";
        public AnimationIndex AnimationIndex { get; private set; }
        public ObjectPathRemapper ObjectPathRemapper { get; private set; }
        public VirtualMotion EmptyMotion { get; private set; }
        public VirtualClip AlwaysOnClip { get; }
        public VirtualBlendTree RootTree { get; }
        public VirtualLayer BaseLayer { get; }
        public VirtualClip BaseLayerClip { get; }
        private readonly VirtualAnimatorController _vac;
        private int _counter;

        public int Latency { get; private set; }
        public int LatencyHorizon { get; private set; }

        public BakeContext(ndmf.BuildContext buildContext, VirtualAnimatorController vac)
        {
            var asc = buildContext.Extension<AnimatorServicesContext>();
            AnimationIndex = asc.AnimationIndex;
            ObjectPathRemapper = asc.ObjectPathRemapper;
            
            EmptyMotion = VirtualClip.Create("Empty");
            _vac = vac;

            AlwaysOnClip = VirtualClip.Create("Base");

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
                Motion = AlwaysOnClip,
                DirectBlendParameter = ALWAYS_ONE
            });

            // Base layer at lowest priority to hold initial active-state defaults
            var baseBlendTree = VirtualBlendTree.Create("BaseLayerTree");
            BaseLayerClip = VirtualClip.Create("BaseLayerClip");
            baseBlendTree.BlendType = BlendTreeType.Direct;
            baseBlendTree.NormalizedBlendValues = false;
            baseBlendTree.UseAutomaticThresholds = false;
            baseBlendTree.Children = baseBlendTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = BaseLayerClip,
                DirectBlendParameter = ALWAYS_ONE
            });

            BaseLayer = vac.AddLayer(new LayerPriority(int.MinValue), BASE_LAYER_NAME);
            BaseLayer.BlendingMode = AnimatorLayerBlendingMode.Override;
            BaseLayer.DefaultWeight = 1;
            var sm = BaseLayer.StateMachine ??
                     throw new InvalidOperationException("Base animator layer was created without a state machine");
            var state = sm.AddState("Base");
            sm.DefaultState = state;
            state.Motion = baseBlendTree;

            var animLayer = vac.AddLayer(new LayerPriority(1), APPLY_LAYER_NAME);
            animLayer.BlendingMode = AnimatorLayerBlendingMode.Override;
            animLayer.DefaultWeight = 1;
            sm = animLayer.StateMachine ??
                 throw new InvalidOperationException("Apply animator layer was created without a state machine");
            state = sm.AddState("Apply");
            sm.DefaultState = state;
            state.Motion = RootTree;
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
            var name = "$$MA/RC/" + prefix + "$" + _counter++;

            SetParameter(name, value);

            return name;
        }

        public void SetParameter(string name, float value)
        {
            var template = new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = value
            };

            _vac.Parameters = _vac.Parameters.SetItem(template.name, template);
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

        public float GetParameterInitialValue(string priorNode)
        {
            return _vac.Parameters.GetValueOrDefault(priorNode)?.defaultFloat ?? 0;
        }

        public void EnsureParameterPresent(string argParameter)
        {
            if (!_vac.Parameters.ContainsKey(argParameter))
            {
                SetParameter(argParameter, 0);
            }
        }
    }
}
