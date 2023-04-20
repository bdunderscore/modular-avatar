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

            if (actionMenus.IsEmpty) return;

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

            var actions = GenerateActions(avatar, actionMenus, parameters);
            parameters.Add(new AnimatorControllerParameter()
            {
                name = DIRECT_BLEND_TREE_PARAM,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1,
            });

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
            if (layerList.Count > 1)
            {
                layerList[1].defaultWeight = 1;
            }

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
                if (!renderer.sharedMesh)  continue;
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

            _context.SaveAsset(clip);

            layer.stateMachine = new AnimatorStateMachine();
            _context.SaveAsset(layer.stateMachine);

            var state = layer.stateMachine.AddState("Base");
            state.motion = clip;
            state.writeDefaultValues = false;

            layer.defaultWeight = 1;
            layer.name = "Write blendshape defaults";

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
            // WD off causes other write default states to fail to write their defaults (facial expressions get stuck
            // on e.g. kikyo)
            rootState.writeDefaultValues = true;

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

            List<VRCExpressionParameters.Parameter> expParameters =
                expParams.parameters?.ToList() ?? new List<VRCExpressionParameters.Parameter>();
            List<BlendTree> blendTrees = new List<BlendTree>();

            Dictionary<ActionController, List<ModularAvatarMenuItem>> groupedItems =
                new Dictionary<ActionController, List<ModularAvatarMenuItem>>();

            foreach (var item in items)
            {
                List<ModularAvatarMenuItem> group;
                if (item.controlGroup)
                {
                    if (!groupedItems.TryGetValue(item.controlGroup, out group))
                    {
                        group = new List<ModularAvatarMenuItem>();
                        groupedItems.Add(item.controlGroup, group);
                    }
                }
                else
                {
                    group = new List<ModularAvatarMenuItem>();
                    groupedItems.Add(item, group);
                }

                group.Add(item);
            }

            Dictionary<MenuCurveBinding, Component> bindings = new Dictionary<MenuCurveBinding, Component>();

            int paramIndex = 0;
            foreach (var kvp in groupedItems)
            {
                // sort default first
                ModularAvatarMenuItem defaultItem = null;
                if (kvp.Key is ControlGroup cg)
                {
                    defaultItem = cg.defaultValue;
                    if (defaultItem == null || defaultItem.controlGroup != cg) defaultItem = null;
                }

                var group = kvp.Value;
                group.Sort((a, b) =>
                    (b == defaultItem).CompareTo(a == defaultItem));

                // Generate parameter
                var paramname = "_MA/A/" + kvp.Key.gameObject.name + "/" + (paramIndex++);

                var expParamType = group.Count > 1
                    ? VRCExpressionParameters.ValueType.Int
                    : VRCExpressionParameters.ValueType.Bool;

                if (defaultItem == null)
                {
                    group.Insert(0, null);
                }

                bool isSaved = kvp.Key.isSavedProp, isSynced = kvp.Key.isSyncedProp;

                expParameters.Add(new VRCExpressionParameters.Parameter()
                {
                    name = paramname,
                    defaultValue = 0, // TODO
                    valueType = expParamType,
                    saved = isSaved,
                    networkSynced = isSynced
                });
                acParameters.Add(new AnimatorControllerParameter()
                {
                    name = paramname,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0, // TODO
                });

                for (int i = 0; i < group.Count; i++)
                {
                    if (group[i] == null) continue;
                    var control = group[i].Control;
                    control.parameter = new VRCExpressionsMenu.Control.Parameter() {name = paramname};
                    control.value = i;
                }

                var blendTree = new BlendTree();
                blendTree.name = paramname;
                blendTree.blendParameter = paramname;
                blendTree.blendType = BlendTreeType.Simple1D;
                blendTree.useAutomaticThresholds = false;

                List<ChildMotion> children = new List<ChildMotion>();

                List<Motion> motions = GenerateMotions(group, bindings, kvp.Key);

                for (int i = 0; i < motions.Count; i++)
                {
                    children.Add(new ChildMotion()
                    {
                        motion = motions[i],
                        position = new Vector2(i, 0),
                        threshold = i - 0.1f,
                        timeScale = 1,
                    });
                    children.Add(new ChildMotion()
                    {
                        motion = motions[i],
                        position = new Vector2(i, 0),
                        threshold = i + 0.1f,
                        timeScale = 1,
                    });
                }

                blendTree.children = children.ToArray();

                _context.SaveAsset(blendTree);
                blendTrees.Add(blendTree);
            }

            expParams.parameters = expParameters.ToArray();
            descriptor.expressionParameters = expParams;

            return blendTrees;
        }

        void MergeCurves(
            IDictionary<MenuCurveBinding, (Component, AnimationCurve)> curves,
            ActionController controller,
            Func<SwitchedMenuAction, IDictionary<MenuCurveBinding, AnimationCurve>> getCurves,
            bool ignoreDuplicates
        )
        {
            if (controller == null) return;

            foreach (var action in controller.GetComponents<SwitchedMenuAction>())
            {
                var newCurves = getCurves(action);

                foreach (var curvePair in newCurves)
                {
                    var binding = curvePair.Key;
                    var curve = curvePair.Value;

                    if (curves.TryGetValue(binding, out var existing))
                    {
                        if (!ignoreDuplicates)
                        {
                            BuildReport.LogFatal("animation_gen.conflict", new object[]
                            {
                                binding,
                                existing.Item1.gameObject.name,
                                existing.Item1.GetType().Name,
                                controller.gameObject.name,
                                controller.GetType().Name
                            }, binding.target, existing.Item1, controller);
                        }
                    }
                    else
                    {
                        curves.Add(binding, (controller, curve));
                    }
                }
            }
        }

        private List<Motion> GenerateMotions(
            List<ModularAvatarMenuItem> items,
            Dictionary<MenuCurveBinding, Component> bindings,
            ActionController controller
        )
        {
            Dictionary<MenuCurveBinding, Component> newBindings = new Dictionary<MenuCurveBinding, Component>();

            Dictionary<MenuCurveBinding, (Component, AnimationCurve)> inactiveCurves =
                new Dictionary<MenuCurveBinding, (Component, AnimationCurve)>();

            if (controller is ControlGroup)
            {
                MergeCurves(inactiveCurves, controller, a => a.GetCurves(), false);
            }

            foreach (var item in items)
            {
                MergeCurves(inactiveCurves, item, a => a.GetInactiveCurves(false), true);
            }

            var inactiveMotion = CurvesToMotion(inactiveCurves);
            var sampleItem = items.FirstOrDefault(i => i != null);
            String groupName = "(unknown group)";
            if (sampleItem != null)
            {
                groupName = (sampleItem.controlGroup != null
                    ? sampleItem.controlGroup.gameObject.name
                    : sampleItem.gameObject.name);
            }

            inactiveMotion.name =
                groupName
                + " (Inactive)";

            List<Motion> motions = new List<Motion>();

            foreach (var item in items)
            {
                Dictionary<MenuCurveBinding, (Component, AnimationCurve)> activeCurves;

                Motion clip;

                if (item == null)
                {
                    activeCurves = inactiveCurves;
                    clip = inactiveMotion;
                }
                else
                {
                    activeCurves = new Dictionary<MenuCurveBinding, (Component, AnimationCurve)>();

                    MergeCurves(activeCurves, item, a => a.GetCurves(), false);
                    foreach (var kvp in inactiveCurves)
                    {
                        if (!activeCurves.ContainsKey(kvp.Key))
                        {
                            activeCurves.Add(kvp.Key, kvp.Value);
                        }
                    }

                    clip = CurvesToMotion(activeCurves);
                    clip.name = groupName + " (" + item.gameObject.name + ")";
                }

                motions.Add(clip);

                foreach (var binding in activeCurves)
                {
                    if (!newBindings.ContainsKey(binding.Key))
                    {
                        newBindings.Add(binding.Key, binding.Value.Item1);
                    }
                }
            }

            foreach (var binding in newBindings)
            {
                if (bindings.TryGetValue(binding.Key, out var bindingValue))
                {
                    BuildReport.LogFatal("animation_gen.duplicate_binding", new object[]
                    {
                        binding.Key
                    }, binding.Value, bindingValue);
                }
                else
                {
                    bindings.Add(binding.Key, binding.Value);
                }
            }

            return motions;
        }

        Motion CurvesToMotion(IDictionary<MenuCurveBinding, (Component, AnimationCurve)> curves)
        {
            var clip = new AnimationClip();
            _context.SaveAsset(clip);
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