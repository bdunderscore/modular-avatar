#if MA_VRCSDK3_AVATARS

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class MergeBlendTreePass : Pass<MergeBlendTreePass>
    {
        internal const string ALWAYS_ONE = "__ModularAvatarInternal/One";
        internal const string BlendTreeLayerName = "ModularAvatar: Merge Blend Tree";

        private AnimatorServicesContext _asc;
        private VirtualBlendTree _rootBlendTree;
        private HashSet<string> _parameterNames;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            ModularAvatarMergeBlendTree.GetMotionBasePathCallback = (mbt, objectBuildContext) =>
            {
                if (mbt.PathMode == MergeAnimatorPathMode.Absolute) return "";

                var buildContext = (ndmf.BuildContext)objectBuildContext;
                var root = mbt.RelativePathRoot.Get(buildContext.AvatarRootTransform);
                if (root == null) root = mbt.gameObject;

                return RuntimeUtil.AvatarRootPath(root);
            };
        }

        protected override void Execute(ndmf.BuildContext context)
        {
            _asc = context.Extension<AnimatorServicesContext>();
            _rootBlendTree = null;
            _parameterNames = new HashSet<string>();

            var fx = _asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

            foreach (var component in
                     context.AvatarRootObject.GetComponentsInChildren<ModularAvatarMergeBlendTree>(true))
            {
                ErrorReport.WithContextObject(component, () => ProcessComponent(context, component));
            }
            
            // always add the ALWAYS_ONE parameter
            fx.Parameters = fx.Parameters.SetItem(ALWAYS_ONE, new AnimatorControllerParameter()
            {
                name = ALWAYS_ONE,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1
            });

            foreach (var name in _parameterNames)
            {
                if (fx.Parameters.TryGetValue(name, out var existingParameter))
                {
                    if (existingParameter.type != AnimatorControllerParameterType.Float)
                    {
                        existingParameter = new AnimatorControllerParameter
                        {
                            type = AnimatorControllerParameterType.Float,
                            name = name,
                            defaultFloat = existingParameter.type switch
                            {
                                AnimatorControllerParameterType.Bool => existingParameter.defaultBool ? 1 : 0,
                                AnimatorControllerParameterType.Int => existingParameter.defaultInt,
                                _ => 0
                            }
                        };
                    }
                }
                else
                {
                    existingParameter = new AnimatorControllerParameter
                    {
                        name = name,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = 0.0f
                    };
                }

                fx.Parameters = fx.Parameters.SetItem(name, existingParameter);
            }
        }

        private void ProcessComponent(BuildContext context, ModularAvatarMergeBlendTree component)
        {
            var virtualBlendTree = _asc.ControllerContext.GetVirtualizedMotion(component);

            if (virtualBlendTree == null)
            {
                ErrorReport.ReportError(Localization.L, ErrorSeverity.NonFatal, "error.merge_blend_tree.missing_tree");
                return;
            }

            foreach (var motion in virtualBlendTree.AllReachableNodes())
            {
                if (motion is VirtualClip clip)
                {
                    if (CheckClip(clip, component)) break;
                }
            }

            var rootBlend = GetRootBlendTree();
            
            rootBlend.Children = rootBlend.Children.Add(new()
            {
                Motion = virtualBlendTree,
                DirectBlendParameter = ALWAYS_ONE,
                Threshold = 1,
                CycleOffset = 1,
                TimeScale = 1,
            });

            foreach (var asset in virtualBlendTree.AllReachableNodes())
            {
                if (asset is VirtualBlendTree bt2)
                {
                    if (!string.IsNullOrEmpty(bt2.BlendParameter) && bt2.BlendType != BlendTreeType.Direct)
                    {
                        _parameterNames.Add(bt2.BlendParameter);
                    }

                    if (bt2.BlendType != BlendTreeType.Direct && bt2.BlendType != BlendTreeType.Simple1D)
                    {
                        if (!string.IsNullOrEmpty(bt2.BlendParameterY))
                        {
                            _parameterNames.Add(bt2.BlendParameterY);
                        }
                    }

                    if (bt2.BlendType == BlendTreeType.Direct)
                    {
                        foreach (var childMotion in bt2.Children)
                        {
                            if (!string.IsNullOrEmpty(childMotion.DirectBlendParameter))
                            {
                                _parameterNames.Add(childMotion.DirectBlendParameter);
                            }
                        }
                    }
                }
            }

            Object.DestroyImmediate(component);
        }

        private bool CheckClip(VirtualClip clip, ModularAvatarMergeBlendTree component)
        {
            foreach (var ecb in clip.GetFloatCurveBindings())
            {
                var keys = clip.GetFloatCurve(ecb);
                if (keys == null || keys.length < 2) continue;

                var firstKey = keys[0];
                for (var i = 0; i < keys.length; i++)
                {
                    var t0 = i == 0 ? -0.5f : keys[i - 1].time;
                    var t1 = keys[i].time;
                    var t2 = keys[i].time + (t1 - t0);

                    var v0 = keys.Evaluate((t0 + t1) / 2);
                    var v1 = keys.Evaluate(t1);
                    var v2 = keys.Evaluate((t1 + t2) / 2);

                    if (Mathf.Approximately(v0, v1) && Mathf.Approximately(v1, v2))
                    {
                        continue;
                    }

                    ErrorReport.ReportError(Localization.L, ErrorSeverity.NonFatal,
                        "error.merge_blend_tree.non_constant_curve", component, clip);
                    return true;
                }
            }

            return false;
        }

        private VirtualBlendTree GetRootBlendTree()
        {
            if (_rootBlendTree != null) return _rootBlendTree;

            var fx = _asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

            var controller = fx.AddLayer(new LayerPriority(int.MinValue), BlendTreeLayerName);
            var stateMachine = controller.StateMachine;
            if (fx == null)
            {
                throw new Exception("FX layer not found");
            }
            
            _rootBlendTree = VirtualBlendTree.Create("Root");
            var state = stateMachine.AddState("State", _rootBlendTree);
            stateMachine.DefaultState = state;
            state.WriteDefaultValues = true;

            var layerControl = ScriptableObject.CreateInstance<ModularAvatarMMDLayerControl>();
            layerControl.DisableInMMDMode = false;
            stateMachine.Behaviours = ImmutableList<StateMachineBehaviour>.Empty.Add(layerControl);
            
            _rootBlendTree.BlendType = BlendTreeType.Direct;
            _rootBlendTree.BlendParameter = ALWAYS_ONE;
            
            return _rootBlendTree;
        }
    }
}

#endif