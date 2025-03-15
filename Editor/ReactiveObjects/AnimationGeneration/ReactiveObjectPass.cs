#if MA_VRCSDK3_AVATARS
#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf.animator;
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

        private VirtualClip _initialStateClip;
        private bool _writeDefaults;
        
        public ReactiveObjectPass(ndmf.BuildContext context)
        {
            this.context = context;
        }

        internal void Execute()
        {
            if (!context.AvatarDescriptor) return;

            // Having a WD OFF layer after WD ON layers can break WD. We match the behavior of the existing states,
            // and if mixed, use WD ON to maximize compatibility.
            var asc = context.Extension<AnimatorServicesContext>();
            _writeDefaults = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX]?.Layers.Any(
                l => l.StateMachine.StateMachines.Any(
                    sm => sm.StateMachine.AllStates().Any(
                        s => s.WriteDefaultValues && s.Motion is not VirtualBlendTree
                    )
                )
            ) ?? true;
            
            var analysis = new ReactiveObjectAnalyzer(context).Analyze(context.AvatarRootObject);

            var shapes = analysis.Shapes;
            var initialStates = analysis.InitialStates;
            
            GenerateActiveSelfProxies(shapes);

            ProcessMeshDeletion(initialStates, shapes);

            ProcessInitialStates(initialStates, shapes);
            ProcessInitialAnimatorVariables(shapes);
            
            foreach (var groups in shapes.Values)
            {
                ProcessShapeKey(groups);
            }
        }

        private void GenerateActiveSelfProxies(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var rpe = context.Extension<ReadablePropertyExtension>();
            
            foreach (var prop in shapes.Keys)
            {
                if (prop.TargetObject is GameObject go && prop.PropertyName == "m_IsActive")
                {
                    // Ensure a proxy exists for each object we're going to be toggling.
                    // TODO: is this still needed?
                    rpe.GetActiveSelfProxy(go);
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
            var asc = context.Extension<AnimatorServicesContext>();
            var rpe = context.Extension<ReadablePropertyExtension>();
            
            // We need to track _two_ initial states: the initial state we'll apply at build time (which applies
            // when animations are disabled) and the animation base state. Confusingly, the animation base state
            // should be the state that is currently applied to the object...

            var clips = asc.AnimationIndex;
            _initialStateClip = clips.GetClipsForObjectPath(ReactiveObjectPrepass.TAG_PATH).FirstOrDefault();

            if (_initialStateClip == null) return;

            _initialStateClip.Name = "Reactive Component Defaults";

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
                else if (key.TargetObject is Component c)
                {
                    componentType = c.GetType();
                    path = RuntimeUtil.RelativePath(context.AvatarRootObject, c.gameObject);
                }
                else
                {
                    throw new InvalidOperationException("Invalid target object: " + key.TargetObject);
                }

                if (!applied)
                {
                    var serializedObject = new SerializedObject(key.TargetObject);
                    var prop = serializedObject.FindProperty(key.PropertyName);

                    var staticState = shapes.GetValueOrDefault(key)?.overrideStaticState ?? initialState;

                    if (prop != null)
                    {
                        switch (prop.propertyType)
                        {
                            case SerializedPropertyType.Boolean:
                                animBaseState = prop.boolValue ? 1.0f : 0.0f;
                                prop.boolValue = (float)staticState > 0.5f;
                                break;
                            case SerializedPropertyType.Float:
                                animBaseState = prop.floatValue;
                                prop.floatValue = (float)staticState;
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

                    _initialStateClip.SetFloatCurve(binding, curve);

                    if (componentType == typeof(GameObject) && key.PropertyName == "m_IsActive")
                    {
                        binding = EditorCurveBinding.FloatCurve(
                            "",
                            typeof(Animator),
                            rpe.GetActiveSelfProxy((GameObject)key.TargetObject)
                        );

                        _initialStateClip.SetFloatCurve(binding, curve);
                    }
                }
                else if (animBaseState is Object obj)
                {
                    var binding = EditorCurveBinding.PPtrCurve(
                        path,
                        componentType,
                        key.PropertyName
                    );

                    _initialStateClip.SetObjectCurve(binding, new[]
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

        private void ProcessMeshDeletion(Dictionary<TargetProp, object> initialStates,
            Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var renderers = initialStates
                .Where(kvp => kvp.Key.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedShapePrefix))
                .Where(kvp => kvp.Key.TargetObject is SkinnedMeshRenderer)
                .Where(kvp => kvp.Value is float f && f > 0.5f)
                // Filter any non-constant keys
                .Where(kvp =>
                {
                    if (!shapes.ContainsKey(kvp.Key))
                    {
                        // Constant value
                        return true;
                    }

                    var lastGroup = shapes[kvp.Key].actionGroups.LastOrDefault();
                    return lastGroup?.IsConstantActive == true && lastGroup.Value is float f && f > 0.5f;
                })
                .GroupBy(kvp => kvp.Key.TargetObject as SkinnedMeshRenderer)
                .Select(grouping => (grouping.Key, grouping.Select(
                    kvp => kvp.Key.PropertyName.Substring(ReactiveObjectAnalyzer.DeletedShapePrefix.Length)
                ).ToList()))
                .ToList();
            foreach (var (renderer, shapeNamesToDelete) in renderers)
            {
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var shapesToDelete = shapeNamesToDelete
                    .Select(shape => mesh.GetBlendShapeIndex(shape))
                    .Where(k => k >= 0)
                    .ToList();

                renderer.sharedMesh = RemoveBlendShapeFromMesh.RemoveBlendshapes(mesh, shapesToDelete);

                foreach (var name in shapeNamesToDelete)
                {
                    // Don't need to animate this anymore...!
                    shapes.Remove(new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = ReactiveObjectAnalyzer.BlendshapePrefix + name
                    });

                    shapes.Remove(new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = ReactiveObjectAnalyzer.DeletedShapePrefix + name
                    });

                    initialStates.Remove(new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = ReactiveObjectAnalyzer.BlendshapePrefix + name
                    });
                }
            }

            // Remove all deletedShape. props to avoid creating animator noise that'll confuse tools like AAO
            foreach (var key in shapes.Keys.ToList())
            {
                if (key.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedShapePrefix))
                {
                    shapes.Remove(key);
                }
            }
        }

        #endregion

        private void ProcessShapeKey(AnimatedProperty info)
        {
            if (info.actionGroups.Count == 0)
            {
                // This is present only to override the static state; skip animation generation
                return;
            }
            
            // TODO: prune non-animated keys
            var asm = GenerateStateMachine(info);
            ApplyController(asm, "MA Responsive: " + info.TargetProp.TargetObject.name);
        }

        private AnimatorStateMachine GenerateStateMachine(AnimatedProperty info)
        {
            var asc = context.Extension<AnimatorServicesContext>();
            var asm = new AnimatorStateMachine();

            // Workaround for the warning: "'.' is not allowed in State name"
            asm.name = "RC " + info.TargetProp.TargetObject.name.Replace(".", "_");

            var x = 200;
            var y = 0;
            var yInc = 60;

            asm.anyStatePosition = new Vector3(-200, 0);
            
            var initialState = new AnimatorState();
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

            var lastConstant = info.actionGroups.FindLastIndex(agk => agk.IsConstant);
            var transitionBuffer = new List<(AnimatorState, List<AnimatorStateTransition>)>();
            var entryTransitions = new List<AnimatorTransition>();

            transitionBuffer.Add((initialState, new List<AnimatorStateTransition>()));

            // Note: We need to generate a group for any base constant state as well; this is because we generate the
            // scene initial value as a base animation curve in the base blend tree, which would be exposed in the
            // default state. This is incorrect when there is a constant-on Object Toggle or similar changing the
            // initial state of a property.
            //
            // We can, however, skip any groups _before_ that constant state, as they'll be overridden in all cases.
            foreach (var group in info.actionGroups.Skip(Math.Max(0, lastConstant - 1)))
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

                    var conditions = GetTransitionConditions(group);

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

            if (initialState.motion == null)
            {
                // For some reason, if we set the state's motion multiple times, Unity will sometimes revert to the
                // first motion set; as such, make sure to set the empty motion only if we really mean it. 
                var initial = new AnimationClip();
                initial.name = "empty motion";
                initialState.motion = initial;
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

        private AnimatorCondition[] GetTransitionConditions(ReactionRule group)
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
                    var rpe = context.Extension<ReadablePropertyExtension>();
                    var propName = rpe.GetActiveSelfProxy(targetObject);
                    binding = EditorCurveBinding.FloatCurve("", typeof(Animator), propName);
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }

            return clip;
        }

        private void ApplyController(AnimatorStateMachine asm, string layerName)
        {
            var asc = context.Extension<AnimatorServicesContext>();
            var fx = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

            if (fx == null)
            {
                throw new InvalidOperationException("No FX layer found");
            }

            foreach (var paramName in initialValues.Keys.Except(fx.Parameters.Keys))
            {
                var parameter = new AnimatorControllerParameter
                {
                    name = paramName,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = initialValues[paramName], // TODO
                };
                fx.Parameters = fx.Parameters.SetItem(paramName, parameter);
            }

            fx.AddLayer(LayerPriority.Default, "RC " + layerName).StateMachine =
                asc.ControllerContext.Clone(asm);
        }

        private VRCAvatarDescriptor.CustomAnimLayer FindFxController()
        {
            var fx = context.AvatarDescriptor.baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);

            return fx;
        }
    }
}

#endif