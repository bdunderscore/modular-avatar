#if MA_VRCSDK3_AVATARS
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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
            var asc = context.Extension<AnimatorServicesContext>();
            var activeProxies = context.GetState<ReadablePropertyExtension.Retained>().proxyProps
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            if (activeProxies.Count == 0) return;

            // Filter any proxies not used in animator transitions
            var usedProxies = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX]
                .AllReachableNodes().OfType<VirtualTransitionBase>()
                .SelectMany(t => t.Conditions)
                .Select(c => c.parameter)
                .ToHashSet();

            foreach (var proxyBinding in activeProxies.ToList())
            {
                if (!usedProxies.Contains(proxyBinding.Value))
                {
                    activeProxies.Remove(proxyBinding.Key);
                }
            }
            
            var fx = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            if (fx == null) return;
            
            var nullMotion = new AnimationClip();
            nullMotion.name = "NullMotion";
            
            var blendTree = new BlendTree();
            blendTree.blendType = BlendTreeType.Direct;
            blendTree.useAutomaticThresholds = false;

            blendTree.children = activeProxies
                .Select(prop => GenerateDelayChild(nullMotion, (prop.Key, prop.Value)))
                .ToArray();

            var layer = fx.AddLayer(LayerPriority.Default, "DelayDisable");
            var state = layer.StateMachine.AddState("DelayDisable");
            layer.StateMachine.DefaultState = state;

            state.WriteDefaultValues = true;
            state.Motion = asc.ControllerContext.Clone(blendTree);

            // Ensure the initial state of readable props matches the actual state of the gameobject
            foreach (var controller in asc.ControllerContext.GetAllControllers())
            {
                foreach (var (binding, prop) in activeProxies)
                {
                    var obj = asc.ObjectPathRemapper.GetObjectForPath(binding.path);

                    if (obj != null && controller.Parameters.TryGetValue(prop, out var p))
                    {
                        p.defaultFloat = obj.activeSelf ? 1 : 0;
                        controller.Parameters = controller.Parameters.SetItem(prop, p);
                    }
                }
            }
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