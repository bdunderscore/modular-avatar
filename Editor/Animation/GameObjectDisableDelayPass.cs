using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.animation
{
    /// <summary>
    ///     This pass delays turning GameObjects OFF by one frame when those objects control a ReadableProperty. This
    ///     ensures that we don't expose hidden meshes when removing articles of clothing, for example.
    /// </summary>
    internal class GameObjectDelayDisablePass : Pass<GameObjectDelayDisablePass>
    {
        protected override void Execute(BuildContext context)
        {
            var asc = context.Extension<AnimationServicesContext>();
            if (!asc.BoundReadableProperties.Any()) return;

            var fx = (AnimatorController)context.AvatarDescriptor.baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;

            if (fx == null) return;

            var blendTree = new BlendTree();
            blendTree.blendType = BlendTreeType.Direct;
            blendTree.useAutomaticThresholds = false;

            blendTree.children = asc.BoundReadableProperties.Select(GenerateDelayChild).ToArray();

            var asm = new AnimatorStateMachine();
            var state = new AnimatorState();
            state.name = "DelayDisable";
            state.motion = blendTree;
            state.writeDefaultValues = true;

            asm.defaultState = state;
            asm.states = new[]
            {
                new ChildAnimatorState
                {
                    state = state,
                    position = Vector3.zero
                }
            };

            fx.layers = fx.layers.Append(new AnimatorControllerLayer
            {
                name = "DelayDisable",
                stateMachine = asm,
                defaultWeight = 1,
                blendingMode = AnimatorLayerBlendingMode.Override
            }).ToArray();
        }

        private ChildMotion GenerateDelayChild((EditorCurveBinding, string) binding)
        {
            var ecb = binding.Item1;
            var prop = binding.Item2;

            var motion = new AnimationClip();
            var curve = new AnimationCurve();
            curve.AddKey(0, 1);
            AnimationUtility.SetEditorCurve(motion, ecb, curve);

            return new ChildMotion
            {
                motion = motion,
                directBlendParameter = prop,
                timeScale = 1
            };
        }
    }
}