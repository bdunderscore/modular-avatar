#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal class DeltaTimeProcessor : IEffectProcessor
    {
        private const int DISPATCH_LATENCY = 5;
        private readonly VRChatBlendTreeBackend Backend;

        public DeltaTimeProcessor(VRChatBlendTreeBackend backend)
        {
            Backend = backend;
        }

        public EffectGroup? Accept(object targetKey, IReadOnlyList<ReactionNode> nodes)
        {
            if (targetKey is not DeltaTimeControlAction.Target) return null;

            var nodesList = nodes.ToList();
            return new EffectGroup(this, targetKey, nodesList,
                DISPATCH_LATENCY + EffectProcessorHelpers.BranchLatency(nodesList.Count));
        }

        public bool CanMerge(EffectGroup a, EffectGroup b)
        {
            return false;
        }

        public IMotionNode ProcessEffectGroup(UnityBlendTreeBackend backend, EffectGroup group)
        {
            var deciderProp = backend.AddUniqueParameter("DTP_BranchIndex", group.DefaultNode ?? 0);

            var deciderGroup = new EffectGroup(
                new SimplePropProcessor(),
                new ParameterTarget(deciderProp),
                group.Nodes.Select((n, i) => new ReactionNode(
                    n.Expression,
                    new DriveParameter(deciderProp, i)
                )).ToList(),
                EffectProcessorHelpers.BranchLatency(group.Nodes.Count)
            );
            deciderGroup.DefaultNode = group.DefaultNode;

            var deciderNode = deciderGroup.Processor.ProcessEffectGroup(backend, deciderGroup)!;

            // We will be returning the decider node directly; all of our custom logic will be on our own
            // layer (to allow for MotionTime control).
            var now = Backend.ElapsedTimeParam;
            var deltaTime = Backend.DeltaTimeParam;
            var syncInit = Backend.SyncInitParam;

            // TODO - we need to prevent transitions when in the initialization phase.
            // Detect initialization complete by TrackingType > 2, or a few seconds passing,
            // plus enough delay for the maximum latency in the graph.
            // Only perform transitions when we have done at least one post-init transition.

            var motions = new List<VirtualMotion?>
            {
                Delay(deciderProp, out var deciderT1),
                Delay(deciderT1, out var deciderT2),
                Delay(deciderT2, out var deciderT3),
                Delay(deciderT3, out var deciderT4),
                Subtract(deciderProp, deciderT1, out var deltaProp), // T=2
                Latch(deltaProp, now, out var transitionStartTime), // T=3
                Subtract(now, transitionStartTime, out var elapsed), // T=4
                DispatchDeltaTime(deciderT4, elapsed) // T=5
            };


            // Suppress the transition if we haven't received our initial sync (or at the moment
            // of initial sync).
            // We expect to receive syncInit at the same time as a possible transition;
            // we therefore need to delay syncInit for _longer_ than the decider variable's
            // total latency.
            // On top of that, we also need two frames for the deltaProp computation.
            var syncInitLatencyTarget = group.Latency + 3;

            var effectiveSyncInit = syncInit;
            for (var i = 0; i < syncInitLatencyTarget; i++)
            {
                motions.Add(MergeDelay(effectiveSyncInit, effectiveSyncInit + "/D"));
                effectiveSyncInit += "/D";
            }

            motions.Add(Latch(deltaProp, effectiveSyncInit, out var syncInitLatch)); // T=3
            motions.Add(SuppressElapsed(elapsed, syncInitLatch)); // T=4
            var layerMotion = MergeMotions(motions.ToArray());

            Backend.SetParameterInitialValue(transitionStartTime, -9999999);
            Backend.SetParameterInitialValue(elapsed, 9999999);

            var layer = Backend.FX.AddLayer(new LayerPriority(int.MaxValue), "MA RC: DeltaTime");
            var state = layer.StateMachine!.AddState("Root");
            state.Motion = layerMotion;
            state.TimeParameter = deltaTime;

            return deciderNode;

            VirtualMotion DispatchDeltaTime(string selector, string elapsed)
            {
                var priorTransition = -0.5f;

                var builder = ImmutableList.CreateBuilder<VirtualBlendTree.VirtualChildMotion>();

                foreach (var (action, index) in group.Nodes.Select((a, i) => (a, i)))
                {
                    var transition = index + 0.5f;
                    var clip = ClipForBranch(action, elapsed);
                    builder.Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = priorTransition.NextLargest(),
                        Motion = clip
                    });
                    builder.Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = transition,
                        Motion = clip
                    });
                    priorTransition = transition;
                }

                var bt = VirtualBlendTree.Create("Dispatch DeltaTime");
                bt.BlendType = BlendTreeType.Simple1D;
                bt.BlendParameter = selector;
                bt.UseAutomaticThresholds = false;
                bt.Children = builder.ToImmutableList();

                return bt;
            }

            VirtualMotion ClipForBranch(ReactionNode node, string elapsed)
            {
                // options for transition
                // 1. lerp smoothing - requires using delta time in our motion time field. Hard to
                // transition to a fully locked state. In this case we'd set the motion time to frame
                // delta time, and 1D-bt on transition elapsed time to shift to a constant state.
                // 2. Preserve prior position - allows precise lerp, but requires an additional constraint
                // Animator logic is simpler, but unclear if it'd actually be any faster.

                // For now, we do frametime-independent lerp smoothing. Transition time is defined as six
                // time constants (τ); after five, we begin slewing over to a fully locked configuration.
                // At this time (5/6 of the transition time), we are 1-e^(-5) = ~99.3% of the way to
                // the target. We then spend one more time constant slewing over to be fully locked in place.

                // The actual motion we need to generate is therefore a piecewise approximation of the
                // function
                //
                //   exp(-ΔT / τ) (or 1- the same)
                //
                // For simplicity, we currently use a two-key approximation, at FPS=15 and FPS=200
                var action = node.Effects.OfType<DeltaTimeControlAction>().LastOrDefault();
                if (action == null || action.TargetObject is not Component targetComponent)
                    return VirtualClip.Create("Empty");

                var targetPath = backend.ObjectPathRemapper.GetVirtualPathForObject(targetComponent.gameObject);
                var targetType = targetComponent.GetType();

                var tau = action.TransitionDuration / 6f;
                var slewStart = tau * 5f;
                var lockedTime = action.TransitionDuration;

                var deltaTimeClip = VirtualClip.Create("Transition");
                var lockedClip = VirtualClip.Create("Locked");
                foreach (var (prop, value) in action.ConstProps)
                {
                    deltaTimeClip.SetFloatCurve(
                        TargetProp(prop),
                        AnimationCurve.Constant(0, 1, value)
                    );
                    lockedClip.SetFloatCurve(
                        TargetProp(prop),
                        AnimationCurve.Constant(0, 1, value)
                    );
                }

                lockedClip.SetFloatCurve(
                    TargetProp(action.SelectedProp),
                    AnimationCurve.Constant(0, 1, 1f)
                );
                lockedClip.SetFloatCurve(
                    TargetProp(action.AntiSelectedProp),
                    AnimationCurve.Constant(0, 1, 0f)
                );

                deltaTimeClip.SetFloatCurve(
                    TargetProp(action.SelectedProp),
                    InterpolationCurve(tau, false)
                );
                deltaTimeClip.SetFloatCurve(
                    TargetProp(action.AntiSelectedProp),
                    InterpolationCurve(tau, true)
                );

                var bt = VirtualBlendTree.Create("Transition " + action.SelectedProp);
                bt.BlendType = BlendTreeType.Simple1D;
                bt.BlendParameter = elapsed;
                bt.UseAutomaticThresholds = false;
                bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = slewStart,
                        Motion = deltaTimeClip
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = lockedTime.NextLargest(),
                        Motion = lockedClip
                    });

                return bt;


                EditorCurveBinding TargetProp(string prop)
                {
                    return EditorCurveBinding.FloatCurve(targetPath, targetType, prop);
                }
            }

            AnimationCurve InterpolationCurve(float tau, bool invert)
            {
                var deltaTimes = new[] { 1, 1f / 15, 1f / 30, 1f / 45, 1f / 90, 1f / 120, 1f / 200, 0 };

                var frames = deltaTimes.OrderBy(a => a).Select(dt =>
                {
                    var value = (float)Math.Exp(-dt / tau);
                    var derivative = value * -(1 / tau);

                    Keyframe kf = new()
                    {
                        value = value,
                        inTangent = derivative,
                        outTangent = derivative,
                        // TODO - find a better approximation here
                        inWeight = 1,
                        outWeight = 1,
                        time = dt,
                        weightedMode = WeightedMode.Both
                    };

                    if (!invert)
                    {
                        kf.value = 1 - kf.value;
                        kf.inTangent = -kf.inTangent;
                        kf.outTangent = -kf.outTangent;
                    }

                    return kf;
                }).ToArray();

                var ac = new AnimationCurve();
                ac.keys = frames;
                ac.preWrapMode = WrapMode.ClampForever;
                ac.postWrapMode = WrapMode.ClampForever;

                return ac;
            }

            VirtualMotion Latch(string controlProp, string input, out string output)
            {
                output = $"{input}/Latch";
                backend.EnsureParameterPresent(output, backend.GetParameterInitialValue(input));

                var copySelf = Copy(output, output);
                var copyFromInput = Copy(input, output);

                var bt = VirtualBlendTree.Create("Latch " + input);
                bt.BlendType = BlendTreeType.Simple1D;
                bt.BlendParameter = controlProp;
                bt.UseAutomaticThresholds = false;

                var threshLo = -0.5f;
                var threshHi = 0.5f;

                bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = threshLo.NextSmallest(),
                        Motion = copyFromInput
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = threshLo,
                        Motion = copySelf
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = threshHi.NextSmallest(),
                        Motion = copySelf
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = threshHi,
                        Motion = copyFromInput
                    });

                return bt;
            }

            VirtualMotion? MergeDelay(string from, string to)
            {
                if (backend.Parameters.HasParameter(to)) return null;
                backend.EnsureParameterPresent(to, backend.GetParameterInitialValue(from));

                var bt = VirtualBlendTree.Create("Delay " + from);
                bt.BlendType = BlendTreeType.Direct;

                var zeroize = VirtualClip.Create("Zero " + to);
                zeroize.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), to),
                    AnimationCurve.Constant(0, 1, 0f)
                );

                var one = VirtualClip.Create("One " + from);
                one.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), to),
                    AnimationCurve.Constant(0, 1, 1f)
                );

                bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = UnityBlendTreeBackend.ALWAYS_ONE,
                        Motion = zeroize
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = from,
                        Motion = one
                    });

                return bt;
            }

            VirtualMotion? Delay(string from, out string to)
            {
                to = from + "/D";

                return MergeDelay(from, to);
            }

            VirtualMotion Copy(string from, string to)
            {
                var bt = VirtualBlendTree.Create("Copy " + from);
                bt.BlendType = BlendTreeType.Direct;

                var zeroize = VirtualClip.Create("Zero " + to);
                zeroize.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), to),
                    AnimationCurve.Constant(0, 1, 0f)
                );

                var one = VirtualClip.Create("One " + from);
                one.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), to),
                    AnimationCurve.Constant(0, 1, 1f)
                );

                bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = UnityBlendTreeBackend.ALWAYS_ONE,
                        Motion = zeroize
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = from,
                        Motion = one
                    });

                return bt;
            }

            VirtualMotion Subtract(string a, string b, out string result)
            {
                result = a + "_minus_" + b;
                backend.EnsureParameterPresent(result,
                    backend.GetParameterInitialValue(a) - backend.GetParameterInitialValue(b));

                var bt = VirtualBlendTree.Create("Subtract " + a + " - " + b);
                bt.BlendType = BlendTreeType.Direct;

                var zeroize = ParamClip(result, 0);
                var pos = ParamClip(result, 1);
                var neg = ParamClip(result, -1);

                bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = UnityBlendTreeBackend.ALWAYS_ONE,
                        Motion = zeroize
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = a,
                        Motion = pos
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        DirectBlendParameter = b,
                        Motion = neg
                    });

                return bt;
            }

            VirtualMotion? SuppressElapsed(string elapsed, string enableFlag)
            {
                var bt = VirtualBlendTree.Create("SuppressElapsed");
                bt.BlendType = BlendTreeType.Simple1D;
                bt.UseAutomaticThresholds = false;
                bt.BlendParameter = enableFlag;

                var zero = ParamClip(elapsed, 0);
                var suppress = ParamClip(elapsed, 9999999);

                bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = 0.5f,
                        Motion = suppress
                    })
                    .Add(new VirtualBlendTree.VirtualChildMotion
                    {
                        Threshold = 0.5f.NextLargest(),
                        Motion = zero
                    });

                return bt;
            }
        }

        private static VirtualClip ParamClip(string paramName, float value)
        {
            var clip = VirtualClip.Create($"{paramName}={value}");
            clip.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), paramName),
                AnimationCurve.Constant(0, 1, value)
            );
            return clip;
        }

        private VirtualMotion MergeMotions(params VirtualMotion?[] motions)
        {
            var bt = VirtualBlendTree.Create("MergeMotions");
            bt.BlendType = BlendTreeType.Direct;

            bt.Children = motions
                .Where(m => m != null)
                .Select(m => new VirtualBlendTree.VirtualChildMotion
                {
                    DirectBlendParameter = UnityBlendTreeBackend.ALWAYS_ONE,
                    Motion = m!
                }).ToImmutableList();

            return bt;
        }

        public void ApplyStaticState(EffectGroup group)
        {
            throw new NotImplementedException();
        }
    }
}