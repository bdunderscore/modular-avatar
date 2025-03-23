#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using BuildContext = nadena.dev.ndmf.BuildContext;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.animation
{
    internal class MMDRelayState
    {
        internal HashSet<VirtualLayer> mmdAffectedOriginalLayers = new();
    }

    internal class MMDRelayEarlyPass : Pass<MMDRelayEarlyPass>
    {
        protected override void Execute(BuildContext context)
        {
            if (!MMDRelayPass.ShouldRun(context)) return;
            
            var asc = context.Extension<AnimatorServicesContext>();
            if (asc.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fx))
            {
                context.GetState<MMDRelayState>().mmdAffectedOriginalLayers = new HashSet<VirtualLayer>(
                    fx.Layers.Skip(1).Take(2)
                );
            }
        }
    }

    /// <summary>
    ///     Many MMD worlds animate the first three FX layers to weight zero. When MA injects new layers, this can hit
    ///     unintended layers (eg the RC base state layer).
    ///     To work around this, we'll inject a layer which will relay its active state into a parameter; then, we add a
    ///     layer to relay this to layers which should be affected. Finally, any layer which _shouldn't_ be affected is
    ///     pushed out of the first three layers by injecting dummy layers.
    /// </summary>
    internal class MMDRelayPass : Pass<MMDRelayPass>
    {
        private const string MMDRelayParam = "__MA/Internal/MMDNotActive";
        internal const string ControlLayerName = "Modular Avatar: MMD Control";
        internal const string DummyLayerName = "Modular Avatar: MMD Dummy";
        internal const string StateNameInitial = "Initial";
        internal const string StateNameNotMMD = "NotMMD";
        internal const string StateNameMMD = "MMD";

        internal static bool ShouldRun(BuildContext context)
        {
            var settings = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarVRChatSettings>(true);
            return settings.FirstOrDefault()?.MMDWorldSupport ?? true;
        }
        
        protected override void Execute(BuildContext context)
        {
            if (!ShouldRun(context)) return;
            
            var asc = context.Extension<AnimatorServicesContext>();
            if (!asc.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fx))
                return;

            var affectedLayers = context.GetState<MMDRelayState>().mmdAffectedOriginalLayers;

            foreach (var layer in fx.Layers)
            {
                if (layer.StateMachine == null) continue;
                
                var rootMMDModeBehaviors = layer.StateMachine.Behaviours
                    .OfType<ModularAvatarMMDLayerControl>()
                    .ToList();
                
                if (rootMMDModeBehaviors.Count == 0) continue;
                if (rootMMDModeBehaviors.Count > 1)
                {
                    ErrorReport.ReportError(Localization.L, ErrorSeverity.Error,
                        "error.mmd.multiple_mmd_mode_behaviors", layer.Name);
                    continue;
                }

                if (rootMMDModeBehaviors[0].DisableInMMDMode)
                {
                    affectedLayers.Add(layer);
                }
                else
                {
                    affectedLayers.Remove(layer);
                }

                layer.StateMachine.Behaviours = layer.StateMachine.Behaviours
                    .Where(b => b is not ModularAvatarMMDLayerControl).ToImmutableList();
                Object.DestroyImmediate(rootMMDModeBehaviors[0]);

                // check for child behaviors
                // TODO: implement filtering on AllReachableNodes
                foreach (var node in layer.AllReachableNodes())
                {
                    if (node is VirtualState state)
                    {
                        if (state.Behaviours.Any(b => b is ModularAvatarMMDLayerControl))
                        {
                            ErrorReport.ReportError(Localization.L, ErrorSeverity.Error,
                                "error.mmd.mmd_mode_in_child_state", layer.Name, state.Name);
                        }
                    }
                    else if (node is VirtualStateMachine vsm)
                    {
                        if (vsm.Behaviours.Any(b => b is ModularAvatarMMDLayerControl))
                        {
                            ErrorReport.ReportError(Localization.L, ErrorSeverity.Error,
                                "error.mmd.mmd_mode_in_child_state_machine", layer.Name, vsm.Name);
                        }
                    }
                }
            }

            var needsAdjustment = fx.Layers.Select((layer, index) => (layer, index))
                .Any(pair => affectedLayers.Contains(pair.layer) != (pair.index < 3 && pair.index != 0));
            if (!needsAdjustment) return;

            var toDisable = fx.Layers.Where(l => affectedLayers.Contains(l))
                .Select(l => l.VirtualLayerIndex)
                .ToList();

            fx.Parameters = fx.Parameters.Add(MMDRelayParam, new AnimatorControllerParameter
            {
                name = MMDRelayParam,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0
            });

            var currentLayers = fx.Layers.ToList();
            var newLayers = new List<VirtualLayer>();

            // Layer zero's weight can't be changed anyway, so leave it where it is.
            newLayers.Add(currentLayers[0]);
            currentLayers.RemoveAt(0);
            newLayers.Add(CreateMMDLayer(fx, toDisable));

            // Add a dummy layer
            var dummy = fx.AddLayer(new LayerPriority(0), DummyLayerName);
            var s = dummy.StateMachine!.DefaultState = dummy.StateMachine.AddState("Dummy");
            s.Motion = VirtualClip.Create("empty");
            newLayers.Add(dummy);

            fx.Layers = newLayers.Concat(currentLayers);
        }

        private static VirtualLayer CreateMMDLayer(VirtualAnimatorController fx, List<int> virtualLayers)
        {
            // We'll reorder this later, so the layer priority doesn't matter
            var mmdControl = fx.AddLayer(new LayerPriority(0), ControlLayerName);
            var stateMachine = mmdControl.StateMachine ?? throw new Exception("No state machine on MMD Control layer");

            var motion = VirtualClip.Create("MMDRelay");
            motion.SetFloatCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), MMDRelayParam),
                AnimationCurve.Constant(0, 1, 1)
            );

            var state_initial = stateMachine.AddState(StateNameInitial);
            state_initial.Motion = motion;

            var state_notmmd = stateMachine.AddState(StateNameNotMMD);
            state_notmmd.Motion = motion;

            var state_mmd = stateMachine.AddState(StateNameMMD);
            state_mmd.Motion = motion;

            var t = VirtualStateTransition.Create();
            t.SetDestination(state_mmd);
            t.Conditions = ImmutableList.Create(new AnimatorCondition
            {
                mode = AnimatorConditionMode.Less,
                parameter = MMDRelayParam,
                threshold = 0.5f
            });

            state_notmmd.Transitions = ImmutableList.Create(t);

            t = VirtualStateTransition.Create();
            t.SetDestination(state_notmmd);
            t.Conditions = ImmutableList.Create(new AnimatorCondition
            {
                mode = AnimatorConditionMode.Greater,
                parameter = MMDRelayParam,
                threshold = 0.5f
            });

            state_mmd.Transitions = ImmutableList.Create(t);

            t = VirtualStateTransition.Create();
            t.SetDestination(state_mmd);
            t.Conditions = ImmutableList.Create(new AnimatorCondition
            {
                mode = AnimatorConditionMode.Less,
                parameter = MMDRelayParam,
                threshold = 0.5f
            });

            state_initial.Transitions = ImmutableList.Create(t);

            stateMachine.DefaultState = state_initial;

            var mmd_behaviors = ImmutableList.CreateBuilder<StateMachineBehaviour>();
            var notmmd_behaviors = ImmutableList.CreateBuilder<StateMachineBehaviour>();

            foreach (var index in virtualLayers)
            {
                var behavior = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
                behavior.layer = index;
                behavior.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                behavior.goalWeight = 0;
                behavior.blendDuration = 0;

                mmd_behaviors.Add(behavior);

                behavior = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
                behavior.layer = index;
                behavior.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                behavior.goalWeight = 1;
                behavior.blendDuration = 0;

                notmmd_behaviors.Add(behavior);
            }

            state_notmmd.Behaviours = notmmd_behaviors.ToImmutable();
            state_mmd.Behaviours = mmd_behaviors.ToImmutable();

            return mmdControl;
        }

        internal static bool IsRelayLayer(string layerName)
        {
            return layerName == ControlLayerName || layerName == DummyLayerName;
        }
    }
}