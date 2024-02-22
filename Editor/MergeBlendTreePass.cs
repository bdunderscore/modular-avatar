#if MA_VRCSDK3_AVATARS

#region

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using nadena.dev.ndmf.util;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MergeBlendTreePass : Pass<MergeBlendTreePass>
    {
        internal const string ALWAYS_ONE = "__ModularAvatarInternal/One";
        internal const string BlendTreeLayerName = "ModularAvatar: Merge Blend Tree";

        private AnimatorController _controller;
        private BlendTree _rootBlendTree;
        private GameObject _mergeHost;
        private HashSet<string> _parameterNames;

        protected override void Execute(ndmf.BuildContext context)
        {
            _rootBlendTree = null;
            _parameterNames = new HashSet<string>();
            _controller = new AnimatorController();
            
            foreach (var component in
                     context.AvatarRootObject.GetComponentsInChildren<ModularAvatarMergeBlendTree>(true))
            {
                ErrorReport.WithContextObject(component, () => ProcessComponent(context, component));
            }

            List<AnimatorControllerParameter> parameters = new List<AnimatorControllerParameter>(_parameterNames.Count + 1);
            if (_mergeHost != null)
            {
                _parameterNames.Remove(ALWAYS_ONE);

                parameters.Add(new AnimatorControllerParameter()
                {
                    name = ALWAYS_ONE,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1
                });
                
                foreach (var name in _parameterNames)
                {
                    parameters.Add(new AnimatorControllerParameter()
                    {
                        name = name,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = 0
                    });
                }

                var paramsAnimator = new AnimatorController();
                paramsAnimator.parameters = parameters.ToArray();

                var paramsComponent = _mergeHost.AddComponent<ModularAvatarMergeAnimator>();
                paramsComponent.animator = paramsAnimator;
                paramsComponent.layerPriority = Int32.MaxValue;
            }
        }

        private void ProcessComponent(ndmf.BuildContext context, ModularAvatarMergeBlendTree component)
        {
            BlendTree componentBlendTree = component.BlendTree as BlendTree;
            
            if (componentBlendTree == null)
            {
                ErrorReport.ReportError(Localization.L, ErrorSeverity.NonFatal, "error.merge_blend_tree.missing_tree");
                return;
            }

            string basePath = null;
            if (component.PathMode == MergeAnimatorPathMode.Relative)
            {
                var root = component.RelativePathRoot.Get(context.AvatarRootTransform);
                if (root == null) root = component.gameObject;
                
                basePath = RuntimeUtil.AvatarRootPath(root) + "/";
            }
            
            var bt = new DeepClone(context).DoClone(componentBlendTree, basePath);
            var rootBlend = GetRootBlendTree(context);
            
            rootBlend.AddChild(bt);
            var children = rootBlend.children;
            children[children.Length - 1].directBlendParameter = ALWAYS_ONE;
            rootBlend.children = children;

            foreach (var asset in bt.ReferencedAssets(includeScene: false))
            {
                if (asset is BlendTree bt2)
                {
                    if (!string.IsNullOrEmpty(bt2.blendParameter) && bt2.blendType != BlendTreeType.Direct)
                    {
                        _parameterNames.Add(bt2.blendParameter);
                    }

                    if (bt2.blendType != BlendTreeType.Direct && bt2.blendType != BlendTreeType.Simple1D)
                    {
                        if (!string.IsNullOrEmpty(bt2.blendParameterY))
                        {
                            _parameterNames.Add(bt2.blendParameterY);
                        }
                    }

                    if (bt2.blendType == BlendTreeType.Direct)
                    {
                        foreach (var childMotion in bt2.children)
                        {
                            if (!string.IsNullOrEmpty(childMotion.directBlendParameter))
                            {
                                _parameterNames.Add(childMotion.directBlendParameter);
                            }
                        }
                    }
                }
            }
        }

        private BlendTree GetRootBlendTree(ndmf.BuildContext context)
        {
            if (_rootBlendTree != null) return _rootBlendTree;
            
            var newController = new AnimatorController();
            var newStateMachine = new AnimatorStateMachine();
            var newState = new AnimatorState();

            _rootBlendTree = new BlendTree();
            _controller = newController;

            newController.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    defaultWeight = 1,
                    name = BlendTreeLayerName,
                    stateMachine = newStateMachine
                }
            };

            newStateMachine.name = "ModularAvatarMergeBlendTree";
            newStateMachine.states = new[]
            {
                new ChildAnimatorState
                {
                    state = newState,
                    position = Vector3.zero
                }
            };
            newStateMachine.defaultState = newState;
            
            newState.writeDefaultValues = true;
            newState.motion = _rootBlendTree;

            _rootBlendTree.blendType = BlendTreeType.Direct;
            _rootBlendTree.blendParameter = ALWAYS_ONE;
            
            var mergeObject = new GameObject("ModularAvatarMergeBlendTree");
            var merger = mergeObject.AddComponent<ModularAvatarMergeAnimator>();
            merger.animator = newController;
            merger.pathMode = MergeAnimatorPathMode.Absolute;
            merger.matchAvatarWriteDefaults = false;
            merger.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merger.deleteAttachedAnimator = false;
            merger.layerPriority = Int32.MinValue;
            
            mergeObject.transform.SetParent(context.AvatarRootTransform, false);
            mergeObject.transform.SetSiblingIndex(0);

            _mergeHost = mergeObject;

            return _rootBlendTree;
        }
    }
}

#endif