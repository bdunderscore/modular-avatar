#nullable enable

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif
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
using BuildContext = nadena.dev.ndmf.BuildContext;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.animation
{
    internal class MMDRelayState
    {
        internal HashSet<VirtualLayer> mmdAffectedOriginalLayers = new();
    }

#if MA_VRCSDK3_AVATARS
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
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
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
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
            var hasAnyOptInMmdLayerControl = false;
            var layersWithMmdControl = new HashSet<VirtualLayer>();

            foreach (var layer in fx.Layers)
            {
                if (layer.StateMachine == null) continue;
                
                var rootMMDModeBehaviors = layer.StateMachine.Behaviours
                    .OfType<ModularAvatarMMDLayerControl>()
                    .ToList();
                
                if (rootMMDModeBehaviors.Count == 0) continue;

                hasAnyOptInMmdLayerControl = rootMMDModeBehaviors.Any(b => b.DisableInMMDMode);
                layersWithMmdControl.Add(layer);
                
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

            // Check for WD OFF states in non-MMD layers when MMD Layer Control is being used
            CheckForWriteDefaultsOn(fx, layersWithMmdControl, hasAnyOptInMmdLayerControl);

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

            // Layer zero's weight can't be changed anyway, so leave it where it is - unless it explicitly opted in
            if (!affectedLayers.Contains(currentLayers[0]))
            {
                newLayers.Add(currentLayers[0]);
                currentLayers.RemoveAt(0);
            }

            // Add a dummy layer
            CreateDummyLayer(fx, newLayers);

            // Add the control/sensor layer. We do this second so it never ends up being the first layer, which isn't
            // disabled. We don't care if it's layer 1 or 2.
            newLayers.Add(CreateMMDLayer(fx, toDisable));

            // Note that if we opted in layer zero, above, then it comes in as layer 2, which doesn't need special handling.
            fx.Layers = newLayers.Concat(currentLayers);
        }

        private static void CreateDummyLayer(VirtualAnimatorController fx, List<VirtualLayer> newLayers)
        {
            var dummy = fx.AddLayer(new LayerPriority(0), DummyLayerName);
            var s = dummy.StateMachine!.DefaultState = dummy.StateMachine.AddState("Dummy");
            s.Motion = VirtualClip.Create("empty");
            newLayers.Add(dummy);
        }

        private static void CheckForWriteDefaultsOn(VirtualAnimatorController fx,
            HashSet<VirtualLayer> layersWithMmdControl, bool hasAnyMmdControl)
        {
            // Only check if MMD Layer Control is being used
            if (!hasAnyMmdControl) return;
            
            var layersWithWdOff = new List<string>();
            
            foreach (var layer in fx.Layers)
            {
                // Skip MMD-generated relay layers
                if (IsRelayLayer(layer.Name)) continue;
                
                // Skip the layer if it has no state machine
                if (layer.StateMachine == null) continue;
                
                // Check all states in this layer for WriteDefaults = false
                var hasWdOffStates = layer.StateMachine.AllStates()
                    .Any(state => !state.WriteDefaultValues);
                
                if (hasWdOffStates)
                {
                    layersWithWdOff.Add(layer.Name);
                }
            }
            
            // Report warning if any layers with WD OFF were found
            if (layersWithWdOff.Count > 0)
            {
                var layerList = string.Join(", ", layersWithWdOff);
                ErrorReport.ReportError(Localization.L, ErrorSeverity.NonFatal,
                    "warning.mmd.wd_off", layerList);
            }
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
#endif
}