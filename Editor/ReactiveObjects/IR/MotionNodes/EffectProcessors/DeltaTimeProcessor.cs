#nullable enable
#if MA_VRCSDK3_AVATARS

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
        private const int DISPATCH_LATENCY = 1;
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
            var deciderProp = backend.AddUniqueParameter("DTP_BranchIndex", group.DefaultNode ?? -1);

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
            // layer (to allow for MotionTime control). We use a normal animator state machine instead of
            // complex blend tree hacks, both to reduce latency, and because we're stuck with an extra layer
            // anyway due to the need to use MotionTime.
            //
            // The animator structure in the custom layer we generate looks a bit like this:
            //    /----> Sync Pending ----------------------v
            //  Entry -> Moving -> Transition -> Locked -> Exit
            //               \--------\---------------------^
            //
            // This is copied for each branch.

            // Suppress the transition if we haven't received our initial sync (or at the moment
            // of initial sync).
            // We therefore delay syncInit longer than the complete aligned graph path, not
            // only this group's local dispatch latency.
            var syncInitLatencyTarget = group.AlignedGraphLatency + 1;

            var now = Backend.ElapsedTimeParam;
            var deltaTime = Backend.DeltaTimeParam;
            var syncInit = Backend.SyncInitParam;
            var customLayer = this.Backend.FX.AddLayer(new LayerPriority(int.MaxValue), "MA RC: DeltaTime Control");
            
            const int xNodeSize = 300;
            const int yNodeSize = 80;
            Vector3 displacement = new Vector3(xNodeSize, yNodeSize, 0);

            var stateMachine = customLayer.StateMachine!;

            stateMachine.EntryPosition = Vector3.zero;
            stateMachine.ExitPosition = Vector3.right * (xNodeSize * 4);
            
            for (int i = 0; i < group.Nodes.Count; i++)
            {
                var node = group.Nodes[i];
                Vector3 groupPos = Vector3.down * yNodeSize * 4 * i;
                var conditions = ImmutableList<AnimatorCondition>.Empty
                    .Add(new() { mode = AnimatorConditionMode.Greater, parameter = deciderProp, threshold = i - 0.5f })
                    .Add(new() { mode = AnimatorConditionMode.Less, parameter = deciderProp, threshold = i + 0.5f });
                var antiConditions =  ImmutableList<AnimatorCondition>.Empty
                    .Add(new() { mode = AnimatorConditionMode.Less, parameter = deciderProp, threshold = i - 0.5f })
                    .Add(new() { mode = AnimatorConditionMode.Greater, parameter = deciderProp, threshold = i + 0.5f });

                ComputeClips(node, out var movingClip, out var lockedClip, out float movingTime, out float transitionTime);
                
                // Primary group
                var moving = stateMachine.AddState($"S{i}: Moving", position: groupPos + new Vector3(xNodeSize, 0, 0));
                moving.Motion = movingClip;
                moving.TimeParameter = deltaTime;
                
                var transition = stateMachine.AddState($"S{i}: Transition", position: groupPos + new Vector3(xNodeSize * 2, 0, 0));
                transition.Motion = movingClip;
                transition.TimeParameter = deltaTime;
                
                var locked = stateMachine.AddState($"S{i}: Locked", position: groupPos + new Vector3(xNodeSize * 3, 0, 0));
                locked.Motion = lockedClip;
                
                // Pre-sync
                var unsynced = stateMachine.AddState($"S{i}: Sync Pending", position: groupPos + new Vector3(xNodeSize * 2, yNodeSize, 0));
                unsynced.Motion = lockedClip;

                if (i == group.DefaultNode || stateMachine.DefaultState == null)
                {
                    stateMachine.DefaultState = unsynced;
                }
                
                // Entry transitions
                CreateEntryTransitions(conditions, moving, unsynced);

                VirtualStateTransition m2t = CreateTransition(moving, transition, conditions);
                m2t.ExitTime = movingTime;
                m2t.HasFixedDuration = true;
                
                VirtualStateTransition t2l = CreateTransition(transition, locked, conditions);
                t2l.ExitTime = transitionTime;
                t2l.HasFixedDuration = true;

                AddExitTransitions(antiConditions, moving, transition, locked, unsynced);
            }

            return deciderNode;

            
            VirtualStateTransition CreateTransition(VirtualState src, VirtualState dst, ImmutableList<AnimatorCondition> conditions)
            {
                var t = VirtualStateTransition.Create();
                t.SetDestination(dst);
                t.Conditions = conditions;
                t.ExitTime = null;

                src.Transitions = src.Transitions.Add(t);

                return t;
            }

            void AddExitTransitions(
                ImmutableList<AnimatorCondition> antiConditions,
                params VirtualState[] states
            )
            {
                foreach (var state in states)
                {
                    foreach (var condition in antiConditions)
                    {
                        var t = VirtualStateTransition.Create();
                        t.SetExitDestination();
                        t.Conditions = ImmutableList<AnimatorCondition>.Empty.Add(condition);
                        t.ExitTime = null;
                        state.Transitions = state.Transitions.Add(t);
                    }
                }
            }

            
            void CreateEntryTransitions(
                ImmutableList<AnimatorCondition> conditions,
                VirtualState moving,
                VirtualState unsynced
            )
            {
                var movingTransition = VirtualTransition.Create();
                movingTransition.Conditions = conditions
                    .Add(new()
                    {
                        parameter = syncInit,
                        mode = AnimatorConditionMode.Greater,
                        threshold = syncInitLatencyTarget
                    });
                movingTransition.SetDestination(moving);

                var unsyncedTransition = VirtualTransition.Create();
                unsyncedTransition.Conditions = conditions
                    .Add(new()
                    {
                        parameter = syncInit,
                        mode = AnimatorConditionMode.Less,
                        threshold = ((float)syncInitLatencyTarget).NextLargest()
                    });
                unsyncedTransition.SetDestination(unsynced);

                stateMachine.EntryTransitions = stateMachine.EntryTransitions
                    .Add(movingTransition)
                    .Add(unsyncedTransition);
            }
            
            void ComputeClips(
                ReactionNode node,
                out VirtualClip movingClip,
                out VirtualClip lockedClip,
                out float movingTime,
                out float transitionTime
            )
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
                {
                    lockedClip = movingClip = VirtualClip.Create("Empty");
                    movingTime = transitionTime = 1;
                    return;
                }

                var targetPath = backend.ObjectPathRemapper.GetVirtualPathForObject(targetComponent.gameObject);
                var targetType = targetComponent.GetType();

                lockedClip = VirtualClip.Create("Locked");
                foreach (var (prop, value) in action.ConstProps)
                {
                    lockedClip.SetFloatCurve(
                        TargetProp(prop),
                        AnimationCurve.Constant(0, 1, value)
                    );
                }

                if (action.SelectedProp != null)
                {
                    lockedClip.SetFloatCurve(
                        TargetProp(action.SelectedProp),
                        AnimationCurve.Constant(0, 1, 1f)
                    );
                }

                lockedClip.SetFloatCurve(
                    TargetProp(action.AntiSelectedProp),
                    AnimationCurve.Constant(0, 1, 0f)
                );
                
                if (action.TransitionDuration <= 0 || action.SelectedProp == null)
                {
                    movingClip = lockedClip;
                    movingTime = transitionTime = 1;
                    return;
                }
                
                var tau = action.TransitionDuration / 6f;
                movingTime = tau * 5f;
                transitionTime = tau;

                movingClip = VirtualClip.Create("Transition");
                foreach (var (prop, value) in action.ConstProps)
                {
                    movingClip.SetFloatCurve(
                        TargetProp(prop),
                        AnimationCurve.Constant(0, 1, value)
                    );
                }

                movingClip.SetFloatCurve(
                    TargetProp(action.SelectedProp),
                    InterpolationCurve(tau, false)
                );
                movingClip.SetFloatCurve(
                    TargetProp(action.AntiSelectedProp),
                    InterpolationCurve(tau, true)
                );
                
                
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
        }

        public void ApplyStaticState(EffectGroup group)
        {
            throw new NotImplementedException();
        }
    }
}
#endif