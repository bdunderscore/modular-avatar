#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using EditorCurveBinding = UnityEditor.EditorCurveBinding;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal partial class ReactiveObjectPass
    {
        private readonly ndmf.BuildContext context;
        private Dictionary<string, float> initialValues = new();

        // Properties that are being driven, either by foreign animations or Object Toggles
        private HashSet<string> activeProps = new();
        
        private AnimationClip _initialStateClip;
        private bool _writeDefaults;
        
        public ReactiveObjectPass(ndmf.BuildContext context)
        {
            this.context = context;
        }

        internal void Execute()
        {
            // Having a WD OFF layer after WD ON layers can break WD. We match the behavior of the existing states,
            // and if mixed, use WD ON to maximize compatibility.
            _writeDefaults = MergeAnimatorProcessor.ProbeWriteDefaults(FindFxController().animatorController as AnimatorController) ?? true;
            
            var analysis = new ReactiveObjectAnalyzer(context).Analyze(context.AvatarRootObject);

            var shapes = analysis.Shapes;
            var initialStates = analysis.InitialStates;
            var deletedShapes = analysis.DeletedShapes;
            
            GenerateActiveSelfProxies(shapes);

            ProcessInitialStates(initialStates, shapes);
            ProcessInitialAnimatorVariables(shapes);
            
            foreach (var groups in shapes.Values)
            {
                ProcessShapeKey(groups);
            }

            ProcessMeshDeletion(deletedShapes);
        }

        private void GenerateActiveSelfProxies(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var asc = context.Extension<AnimationServicesContext>();
            
            foreach (var prop in shapes.Keys)
            {
                if (prop.TargetObject is GameObject go && prop.PropertyName == "m_IsActive")
                {
                    // Ensure a proxy exists for each object we're going to be toggling.
                    // TODO: is this still needed?
                    asc.GetActiveSelfProxy(go);
                }
            }
        }

        private void ProcessInitialAnimatorVariables(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            foreach (var group in shapes.Values)
            foreach (var agk in group.actionGroups)
            foreach (var condition in agk.ControllingConditions)
            {
                if (condition.IsConstant) continue;

                if (!initialValues.TryGetValue(condition.Parameter, out var curVal) || curVal < -999f)
                {
                    initialValues[condition.Parameter] = condition.InitialValue;
                }
            }
        }

        private void ProcessInitialStates(Dictionary<TargetProp, object> initialStates,
            Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var asc = context.Extension<AnimationServicesContext>();
            
            // We need to track _two_ initial states: the initial state we'll apply at build time (which applies
            // when animations are disabled) and the animation base state. Confusingly, the animation base state
            // should be the state that is currently applied to the object...
            
            var clips = context.Extension<AnimationServicesContext>().AnimationDatabase;
            var initialStateHolder = clips.ClipsForPath(ReactiveObjectPrepass.TAG_PATH).FirstOrDefault();
            if (initialStateHolder == null) return;

            _initialStateClip = new AnimationClip();
            _initialStateClip.name = "Reactive Component Defaults";
            initialStateHolder.CurrentClip = _initialStateClip;

            foreach (var (key, initialState) in initialStates)
            {
                string path;
                Type componentType;

                var applied = false;
                object animBaseState = (float) 0;
                
                if (key.TargetObject is GameObject go)
                {
                    path = RuntimeUtil.RelativePath(context.AvatarRootObject, go);
                    componentType = typeof(GameObject);
                }
                else if (key.TargetObject is Renderer r)
                {
                    path = RuntimeUtil.RelativePath(context.AvatarRootObject, r.gameObject);
                    componentType = r.GetType();

                    if (r is SkinnedMeshRenderer smr && key.PropertyName.StartsWith("blendShape."))
                    {
                        var blendShape = key.PropertyName.Substring("blendShape.".Length);
                        var index = smr.sharedMesh?.GetBlendShapeIndex(blendShape);

                        if (index != null && index >= 0)
                        {
                            animBaseState = smr.GetBlendShapeWeight(index.Value);
                            smr.SetBlendShapeWeight(index.Value, (float) initialState);
                        }

                        applied = true;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Invalid target object: " + key.TargetObject);
                }

                if (!applied)
                {
                    var serializedObject = new SerializedObject(key.TargetObject);
                    var prop = serializedObject.FindProperty(key.PropertyName);

                    if (prop != null)
                    {
                        switch (prop.propertyType)
                        {
                            case SerializedPropertyType.Boolean:
                                animBaseState = prop.boolValue ? 1.0f : 0.0f;
                                prop.boolValue = ((float)initialState) > 0.5f;
                                break;
                            case SerializedPropertyType.Float:
                                animBaseState = prop.floatValue;
                                prop.floatValue = (float) initialState;
                                break;
                            case SerializedPropertyType.ObjectReference:
                                animBaseState = prop.objectReferenceValue;
                                prop.objectReferenceValue = (Object) initialState;
                                break;
                        }

                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                if (!shapes.ContainsKey(key))
                    // Do not generate any animation base state if the property is set to a constant value,
                    // because we won't generate any override layers.
                    continue;

                if (animBaseState is float f)
                {
                    var binding = EditorCurveBinding.FloatCurve(
                        path,
                        componentType,
                        key.PropertyName
                    );
                    
                    var curve = new AnimationCurve();
                    curve.AddKey(0, f);
                    curve.AddKey(1, f);

                    AnimationUtility.SetEditorCurve(_initialStateClip, binding, curve);

                    if (componentType == typeof(GameObject) && key.PropertyName == "m_IsActive")
                    {
                        binding = EditorCurveBinding.FloatCurve(
                            "",
                            typeof(Animator),
                            asc.GetActiveSelfProxy((GameObject)key.TargetObject)
                        );

                        AnimationUtility.SetEditorCurve(_initialStateClip, binding, curve);
                    }
                }
                else if (animBaseState is Object obj)
                {
                    var binding = EditorCurveBinding.PPtrCurve(
                        path,
                        componentType,
                        key.PropertyName
                    );
                    
                    AnimationUtility.SetObjectReferenceCurve(_initialStateClip, binding, new []
                    {
                        new ObjectReferenceKeyframe()
                        {
                            value = obj,
                            time = 0
                        },
                        new ObjectReferenceKeyframe()
                        {
                            value = obj,
                            time = 1
                        }
                    });
                }
            }
        }

        #region Mesh processing

        private void ProcessMeshDeletion(HashSet<TargetProp> deletedKeys)
        {
            ImmutableDictionary<SkinnedMeshRenderer, List<TargetProp>> renderers = deletedKeys
                .GroupBy(
                    v => (SkinnedMeshRenderer) v.TargetObject
                ).ToImmutableDictionary(
                    g => (SkinnedMeshRenderer) g.Key,
                    g => g.ToList()
                );

            foreach (var (renderer, infos) in renderers)
            {
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                renderer.sharedMesh = RemoveBlendShapeFromMesh.RemoveBlendshapes(
                    mesh,
                    infos
                        .Select(i => mesh.GetBlendShapeIndex(i.PropertyName.Substring("blendShape.".Length)))
                        .Where(k => k >= 0)
                        .ToList()
                );
            }
        }

        #endregion

        private void ProcessShapeKey(AnimatedProperty info)
        {
            // TODO: prune non-animated keys

            // Check if this is non-animated and skip most processing if so
            if (info.alwaysDeleted || info.actionGroups[^1].IsConstant) return;

            var asm = GenerateStateMachine(info);
            ApplyController(asm, "MA Responsive: " + info.TargetProp.TargetObject.name);
        }

        private AnimatorStateMachine GenerateStateMachine(AnimatedProperty info)
        {
            var asc = context.Extension<AnimationServicesContext>();
            var asm = new AnimatorStateMachine();

            // Workaround for the warning: "'.' is not allowed in State name"
            asm.name = "RC " + info.TargetProp.TargetObject.name.Replace(".", "_");

            var x = 200;
            var y = 0;
            var yInc = 60;

            asm.anyStatePosition = new Vector3(-200, 0);

            var initial = new AnimationClip();
            var initialState = new AnimatorState();
            initialState.motion = initial;
            initialState.writeDefaultValues = _writeDefaults;
            initialState.name = "<default>";
            asm.defaultState = initialState;

            asm.entryPosition = new Vector3(0, 0);

            var states = new List<ChildAnimatorState>();
            states.Add(new ChildAnimatorState
            {
                position = new Vector3(x, y),
                state = initialState
            });
            asc.AnimationDatabase.RegisterState(states[^1].state);

            var lastConstant = info.actionGroups.FindLastIndex(agk => agk.IsConstant);
            var transitionBuffer = new List<(AnimatorState, List<AnimatorStateTransition>)>();
            var entryTransitions = new List<AnimatorTransition>();

            transitionBuffer.Add((initialState, new List<AnimatorStateTransition>()));

            foreach (var group in info.actionGroups.Skip(lastConstant))
            {
                y += yInc;

                var clip = AnimResult(group.TargetProp, group.Value);

                if (group.IsConstant)
                {
                    clip.name = "Property Overlay constant " + group.Value;
                    initialState.motion = clip;
                }
                else
                {
                    clip.name = "Property Overlay controlled by " + group.ControllingConditions[0].DebugName + " " +
                                group.Value;

                    var conditions = GetTransitionConditions(asc, group);

                    foreach (var (st, transitions) in transitionBuffer)
                    {
                        if (!group.Inverted)
                        {
                            var transition = new AnimatorStateTransition
                            {
                                isExit = true,
                                hasExitTime = false,
                                duration = 0,
                                hasFixedDuration = true,
                                conditions = (AnimatorCondition[])conditions.Clone()
                            };
                            transitions.Add(transition);
                        }
                        else
                        {
                            foreach (var cond in conditions)
                            {
                                transitions.Add(new AnimatorStateTransition
                                {
                                    isExit = true,
                                    hasExitTime = false,
                                    duration = 0,
                                    hasFixedDuration = true,
                                    conditions = new[] { InvertCondition(cond) }
                                });
                            }
                        }
                    }

                    var state = new AnimatorState();

                    // Workaround for the warning: "'.' is not allowed in State name"
                    state.name = group.ControllingConditions[0].DebugName.Replace(".", "_");

                    state.motion = clip;
                    state.writeDefaultValues = _writeDefaults;
                    states.Add(new ChildAnimatorState
                    {
                        position = new Vector3(x, y),
                        state = state
                    });
                    asc.AnimationDatabase.RegisterState(states[^1].state);

                    var transitionList = new List<AnimatorStateTransition>();
                    transitionBuffer.Add((state, transitionList));

                    if (!group.Inverted)
                    {
                        entryTransitions.Add(new AnimatorTransition
                        {
                            destinationState = state,
                            conditions = conditions
                        });

                        foreach (var cond in conditions)
                        {
                            var inverted = InvertCondition(cond);
                            transitionList.Add(new AnimatorStateTransition
                            {
                                isExit = true,
                                hasExitTime = false,
                                duration = 0,
                                hasFixedDuration = true,
                                conditions = new[] { inverted }
                            });
                        }
                    }
                    else
                    {
                        // inverted condition
                        foreach (var cond in conditions)
                        {
                            entryTransitions.Add(new AnimatorTransition()
                            {
                                destinationState = state,
                                conditions = new[] { InvertCondition(cond) }
                            });
                        }
                        
                        transitionList.Add(new AnimatorStateTransition
                        {
                            isExit = true,
                            hasExitTime = false,
                            duration = 0,
                            hasFixedDuration = true,
                            conditions = conditions
                        });
                    }
                }
            }

            foreach (var (st, transitions) in transitionBuffer) st.transitions = transitions.ToArray();

            asm.states = states.ToArray();
            entryTransitions.Reverse();
            asm.entryTransitions = entryTransitions.ToArray();
            asm.exitPosition = new Vector3(500, 0);

            return asm;
        }

        private static AnimatorCondition InvertCondition(AnimatorCondition cond)
        {
            return new AnimatorCondition
            {
                parameter = cond.parameter,
                mode = cond.mode == AnimatorConditionMode.Greater
                    ? AnimatorConditionMode.Less
                    : AnimatorConditionMode.Greater,
                threshold = cond.threshold
            };
        }

        private AnimatorCondition[] GetTransitionConditions(AnimationServicesContext asc, ReactionRule group)
        {
            var conditions = new List<AnimatorCondition>();

            foreach (var condition in group.ControllingConditions)
            {
                if (condition.IsConstant) continue;

                if (float.IsFinite(condition.ParameterValueLo))
                {
                    conditions.Add(new AnimatorCondition
                    {
                        parameter = condition.Parameter,
                        mode = AnimatorConditionMode.Greater,
                        threshold = condition.ParameterValueLo
                    });
                }

                if (float.IsFinite(condition.ParameterValueHi))
                {
                    conditions.Add(new AnimatorCondition
                    {
                        parameter = condition.Parameter,
                        mode = AnimatorConditionMode.Less,
                        threshold = condition.ParameterValueHi
                    });
                }
            }

            if (conditions.Count == 0)
                throw new InvalidOperationException("No controlling parameters found for " + group);

            return conditions.ToArray();
        }

        private Motion AnimResult(TargetProp key, object value)
        {
            string path;
            Type componentType;
            
            if (key.TargetObject is GameObject go)
            {
                path = RuntimeUtil.RelativePath(context.AvatarRootObject, go);
                componentType = typeof(GameObject);
            }
            else if (key.TargetObject is Renderer r)
            {
                path = RuntimeUtil.RelativePath(context.AvatarRootObject, r.gameObject);
                componentType = r.GetType();
            }
            else
            {
                throw new InvalidOperationException("Invalid target object: " + key.TargetObject);
            }

            var clip = new AnimationClip();
            clip.name = $"Set {path}:{key.PropertyName}={value}";

            if (value is Object obj)
            {
                var binding = EditorCurveBinding.PPtrCurve(path, componentType, key.PropertyName);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new []
                {
                    new ObjectReferenceKeyframe()
                    {
                        value = obj,
                        time = 0
                    },
                    new ObjectReferenceKeyframe()
                    {
                        value = obj,
                        time = 1
                    }
                });
            }
            else
            {
                var curve = new AnimationCurve();
                curve.AddKey(0, (float) value);
                curve.AddKey(1, (float) value);

                var binding = EditorCurveBinding.FloatCurve(path, componentType, key.PropertyName);
                AnimationUtility.SetEditorCurve(clip, binding, curve);

                if (key.TargetObject is GameObject targetObject && key.PropertyName == "m_IsActive")
                {
                    var asc = context.Extension<AnimationServicesContext>();
                    var propName = asc.GetActiveSelfProxy(targetObject);
                    binding = EditorCurveBinding.FloatCurve("", typeof(Animator), propName);
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }

            return clip;
        }

        private void ApplyController(AnimatorStateMachine asm, string layerName)
        {
            var fx = FindFxController();
            
            if (fx.animatorController == null)
            {
                throw new InvalidOperationException("No FX layer found");
            }
            
            if (!context.IsTemporaryAsset(fx.animatorController))
            {
                throw new InvalidOperationException("FX layer is not a temporary asset");
            }

            if (!(fx.animatorController is AnimatorController animController))
            {
                throw new InvalidOperationException("FX layer is not an animator controller");
            }

            var paramList = animController.parameters.ToList();
            var paramSet = paramList.Select(p => p.name).ToHashSet();

            foreach (var paramName in initialValues.Keys.Except(paramSet))
            {
                paramList.Add(new AnimatorControllerParameter()
                {
                    name = paramName,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = initialValues[paramName], // TODO
                });
                paramSet.Add(paramName);
            }

            animController.parameters = paramList.ToArray();

            animController.layers = animController.layers.Append(
                new AnimatorControllerLayer
                {
                    stateMachine = asm,
                    name = "RC " + layerName,
                    defaultWeight = 1
                }
            ).ToArray();
        }

        private VRCAvatarDescriptor.CustomAnimLayer FindFxController()
        {
            var fx = context.AvatarDescriptor.baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);

            return fx;
        }
    }
}
