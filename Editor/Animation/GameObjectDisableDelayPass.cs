#if MA_VRCSDK3_AVATARS
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using BuildContext = nadena.dev.ndmf.BuildContext;

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

            var nullMotion = new AnimationClip();
            nullMotion.name = "NullMotion";
            
            var blendTree = new BlendTree();
            blendTree.blendType = BlendTreeType.Direct;
            blendTree.useAutomaticThresholds = false;

            blendTree.children = asc.BoundReadableProperties
                .Select(prop => GenerateDelayChild(nullMotion, prop))
                .ToArray();

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

            // Ensure the initial state of readable props matches the actual state of the gameobject
            var parameters = fx.parameters;
            var paramToIndex = parameters.Select((p, i) => (p, i)).ToDictionary(x => x.p.name, x => x.i);
            foreach (var (binding, prop) in asc.BoundReadableProperties)
            {
                var obj = asc.PathMappings.PathToObject(binding.path);

                if (obj != null && paramToIndex.TryGetValue(prop, out var index))
                {
                    parameters[index].defaultFloat = obj.activeSelf ? 1 : 0;
                }
            }

            fx.parameters = parameters;
        }

        private ChildMotion GenerateDelayChild(Motion nullMotion, (EditorCurveBinding, string) binding)
        {
            var ecb = binding.Item1;
            var prop = binding.Item2;

            var motion = new AnimationClip();
            var curve = new AnimationCurve();
            curve.AddKey(0, 1);
            AnimationUtility.SetEditorCurve(motion, ecb, curve);

            // Occasionally, we'll have a very small value pop up, probably due to FP errors.
            // To correct for this, instead of directly using the property in the direct blend tree,
            // we'll use a 1D blend tree to give ourselves a buffer.

            var bufferBlendTree = new BlendTree();
            bufferBlendTree.blendType = BlendTreeType.Simple1D;
            bufferBlendTree.useAutomaticThresholds = false;
            bufferBlendTree.blendParameter = prop;
            bufferBlendTree.children = new[]
            {
                new ChildMotion
                {
                    motion = nullMotion,
                    timeScale = 1,
                    threshold = 0
                },
                new ChildMotion
                {
                    motion = nullMotion,
                    timeScale = 1,
                    threshold = 0.01f
                },
                new ChildMotion
                {
                    motion = motion,
                    timeScale = 1,
                    threshold = 1
                }
            };

            return new ChildMotion
            {
                motion = bufferBlendTree,
                directBlendParameter = MergeBlendTreePass.ALWAYS_ONE,
                timeScale = 1
            };
        }
    }
}
#endif