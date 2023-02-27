using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ActionGenerator
    {
        private const string DIRECT_BLEND_TREE_PARAM = "_MA/ONE";
        private readonly BuildContext _context;

        public ActionGenerator(BuildContext context)
        {
            _context = context;
        }

        public void OnPreprocessAvatar(VRCAvatarDescriptor avatar)
        {
            // Locate MenuActions
            var actionMenus = avatar.GetComponentsInChildren<MenuAction>(true)
                .Select(a => ((Component) a).gameObject.GetComponent<ModularAvatarMenuItem>())
                .Where(item => item != null)
                .ToImmutableHashSet();

            // Generate the root blendtree and animation; insert into the FX layer
            var animLayers = avatar.baseAnimationLayers;
            int fxLayerIndex = -1;
            AnimatorController controller = null;

            // TODO: refactor out layer manipulation here (+ the base state generator)

            for (int i = 0; i < animLayers.Length; i++)
            {
                if (animLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    fxLayerIndex = i;
                    controller = _context.DeepCloneAnimator(animLayers[i].animatorController);
                    break;
                }
            }

            if (controller == null)
            {
                controller = new AnimatorController();
                controller.name = "FX Controller";
                _context.SaveAsset(controller);
            }

            animLayers[fxLayerIndex].animatorController = controller;
            avatar.baseAnimationLayers = animLayers;

            var parameters = controller.parameters.ToList();
            parameters.Add(new AnimatorControllerParameter()
            {
                name = DIRECT_BLEND_TREE_PARAM,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1,
            });

            var actions = GenerateActions(avatar, actionMenus, parameters);

            controller.parameters = parameters.ToArray();

            int layersToInsert = 2; // TODO

            var rootBlendTree = GenerateRootBlendLayer(actions);
            AdjustAllBehaviors(controller, b =>
            {
                if (b is VRCAnimatorLayerControl lc && lc.playable == VRC_AnimatorLayerControl.BlendableLayer.FX)
                {
                    lc.layer += layersToInsert;
                }
            });
            foreach (var layer in controller.layers)
            {
                layer.syncedLayerIndex += layersToInsert;
            }

            var layerList = controller.layers.ToList();
            layerList.Insert(0, GenerateBlendshapeBaseLayer(avatar));
            rootBlendTree.defaultWeight = 1;
            layerList.Insert(0, rootBlendTree);
            layerList[2].defaultWeight = 1;
            controller.layers = layerList.ToArray();

            foreach (var action in avatar.GetComponentsInChildren<MenuAction>(true))
            {
                Object.DestroyImmediate((UnityEngine.Object) action);
            }
        }

        private AnimatorControllerLayer GenerateBlendshapeBaseLayer(VRCAvatarDescriptor avatar)
        {
            AnimatorControllerLayer layer = new AnimatorControllerLayer();
            AnimationClip clip = new AnimationClip();
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int nShapes = renderer.sharedMesh.blendShapeCount;
                for (int i = 0; i < nShapes; i++)
                {
                    float value = renderer.GetBlendShapeWeight(i);
                    if (value > 0.000001f)
                    {
                        clip.SetCurve(
                            RuntimeUtil.AvatarRootPath(renderer.gameObject),
                            typeof(SkinnedMeshRenderer),
                            "blendShape." + renderer.sharedMesh.GetBlendShapeName(i),
                            AnimationCurve.Constant(0, 1, value)
                        );
                    }
                }
            }

            layer.stateMachine = new AnimatorStateMachine();
            _context.SaveAsset(layer.stateMachine);

            var state = layer.stateMachine.AddState("Base");
            state.motion = clip;
            state.writeDefaultValues = false;

            layer.defaultWeight = 1;

            return layer;
        }

        private void AdjustAllBehaviors(AnimatorController controller, Action<StateMachineBehaviour> action)
        {
            HashSet<object> visited = new HashSet<object>();
            foreach (var layer in controller.layers)
            {
                VisitStateMachine(layer.stateMachine);
            }

            void VisitStateMachine(AnimatorStateMachine layerStateMachine)
            {
                if (!visited.Add(layerStateMachine)) return;
                foreach (var state in layerStateMachine.states)
                {
                    foreach (var behaviour in state.state.behaviours)
                    {
                        action(behaviour);
                    }
                }

                foreach (var child in layerStateMachine.stateMachines)
                {
                    VisitStateMachine(child.stateMachine);
                }
            }
        }

        private AnimatorControllerLayer GenerateRootBlendLayer(List<BlendTree> actions)
        {
            var motion = new BlendTree();
            motion.name = "Menu Actions (generated)";
            motion.blendParameter = DIRECT_BLEND_TREE_PARAM;
            motion.blendType = BlendTreeType.Direct;
            motion.children = actions.Select(a => new ChildMotion()
            {
                motion = a,
                directBlendParameter = DIRECT_BLEND_TREE_PARAM,
                timeScale = 1,
            }).ToArray();

            var layer = new AnimatorControllerLayer();
            layer.name = "Menu Actions (generated)";
            layer.defaultWeight = 1;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.stateMachine = new AnimatorStateMachine();
            layer.stateMachine.name = "Menu Actions (generated)";
            //layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            _context.SaveAsset(layer.stateMachine);

            var rootState = layer.stateMachine.AddState("Root");
            rootState.motion = motion;

            return layer;
        }

        private List<BlendTree> GenerateActions(
            VRCAvatarDescriptor descriptor,
            IEnumerable<ModularAvatarMenuItem> items,
            List<AnimatorControllerParameter> acParameters)
        {
            var expParams = descriptor.expressionParameters;
            if (expParams != null)
            {
                expParams = Object.Instantiate(expParams);
                _context.SaveAsset(expParams);
            }
            else
            {
                expParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                expParams.name = "Expression Parameters";
                _context.SaveAsset(expParams);
                descriptor.expressionParameters = expParams;
            }

            List<VRCExpressionParameters.Parameter> expParameters = expParams.parameters.ToList();
            List<BlendTree> blendTrees = new List<BlendTree>();

            int index = 0;
            foreach (var item in items)
            {
                var paramname = "_MA/A/" + item.gameObject.name + "/" + (index++);
                // TODO toggle group handling
                expParameters.Add(new VRCExpressionParameters.Parameter()
                {
                    name = paramname,
                    defaultValue = 0, // TODO
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    saved = false, // TODO
                });
                acParameters.Add(new AnimatorControllerParameter()
                {
                    name = paramname,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0, // TODO
                });

                item.Control.parameter = new VRCExpressionsMenu.Control.Parameter() {name = paramname};
                item.Control.value = 1;

                var blendTree = new BlendTree();
                blendTree.name = paramname;
                blendTree.blendParameter = paramname;
                blendTree.blendType = BlendTreeType.Simple1D;
                blendTree.useAutomaticThresholds = false;
                blendTree.children = new[]
                {
                    new ChildMotion()
                    {
                        motion = GenerateMotion(item, false),
                        position = new Vector2(0, 0),
                        threshold = 0.25f,
                        timeScale = 1,
                    },
                    new ChildMotion()
                    {
                        motion = GenerateMotion(item, true),
                        position = new Vector2(1, 0),
                        threshold = 0.75f,
                        timeScale = 1,
                    },
                };

                _context.SaveAsset(blendTree);
                blendTrees.Add(blendTree);
            }

            expParams.parameters = expParameters.ToArray();
            descriptor.expressionParameters = expParams;

            return blendTrees;
        }

        private Motion GenerateMotion(ModularAvatarMenuItem item, bool active)
        {
            AnimationClip clip = new AnimationClip();
            _context.SaveAsset(clip);
            clip.name = item.gameObject.name + (active ? " (On)" : " (Off)");

            Dictionary<MenuCurveBinding, (Component, AnimationCurve)> curves =
                new Dictionary<MenuCurveBinding, (Component, AnimationCurve)>();

            foreach (var action in item.GetComponents<MenuAction>())
            {
                var newCurves = active ? action.GetCurves() : action.GetDefaultCurves();

                foreach (var curvePair in newCurves)
                {
                    var binding = curvePair.Key;
                    var curve = curvePair.Value;

                    if (curves.TryGetValue(binding, out var existing))
                    {
                        if (active)
                        {
                            BuildReport.LogFatal("animation_gen.conflict", new object[]
                            {
                                binding,
                                existing.Item1.gameObject.name,
                                existing.Item1.GetType().Name,
                                item.gameObject.name,
                                item.GetType().Name
                            }, binding.target, existing.Item1, item);
                        }
                    }
                    else
                    {
                        curves.Add(binding, (item, curve));
                    }
                }
            }

            foreach (var entry in curves)
            {
                clip.SetCurve(
                    RuntimeUtil.AvatarRootPath(entry.Key.target),
                    entry.Key.type,
                    entry.Key.property,
                    entry.Value.Item2 // curve
                );
            }

            return clip;
        }
    }
}