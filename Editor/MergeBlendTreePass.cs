#if MA_VRCSDK3_AVATARS

#region

using System;
using System.Collections.Generic;
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
                if (fx.Parameters.ContainsKey(name)) continue;
                
                fx.Parameters = fx.Parameters.SetItem(name, new AnimatorControllerParameter()
                {
                    name = name,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0.0f
                });
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
            
            _rootBlendTree.BlendType = BlendTreeType.Direct;
            _rootBlendTree.BlendParameter = ALWAYS_ONE;
            
            return _rootBlendTree;
        }
    }
}

#endif