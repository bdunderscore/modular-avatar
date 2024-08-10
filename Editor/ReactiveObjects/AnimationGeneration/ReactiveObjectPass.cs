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
        
        public ReactiveObjectPass(ndmf.BuildContext context)
        {
            this.context = context;
        }

        internal void Execute()
        {
            Dictionary<TargetProp, AnimatedProperty> shapes = FindShapes(context);
            FindObjectToggles(shapes, context);
            FindMaterialSetters(shapes, context);

            AnalyzeConstants(shapes);
            ResolveToggleInitialStates(shapes);
            PreprocessShapes(shapes, out var initialStates, out var deletedShapes);
            
            ProcessInitialStates(initialStates);
            ProcessInitialAnimatorVariables(shapes);
            
            foreach (var groups in shapes.Values)
            {
                ProcessShapeKey(groups);
            }

            ProcessMeshDeletion(deletedShapes);
        }

        private void AnalyzeConstants(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var asc = context.Extension<AnimationServicesContext>();
            HashSet<GameObject> toggledObjects = new();

            foreach (var targetProp in shapes.Keys)
                if (targetProp is { TargetObject: GameObject go, PropertyName: "m_IsActive" })
                    toggledObjects.Add(go);

            foreach (var group in shapes.Values)
            {
                foreach (var actionGroup in group.actionGroups)
                {
                    foreach (var condition in actionGroup.ControllingConditions)
                        if (condition.ReferenceObject != null && !toggledObjects.Contains(condition.ReferenceObject))
                            condition.IsConstant = asc.AnimationDatabase.ClipsForPath(asc.PathMappings.GetObjectIdentifier(condition.ReferenceObject)).IsEmpty;

                    var i = 0;
                    // Remove redundant conditions
                    actionGroup.ControllingConditions.RemoveAll(c => c.IsConstant && c.InitiallyActive && (i++ != 0));
                }

                // Remove any action groups with always-off conditions
                group.actionGroups.RemoveAll(agk =>
                    agk.ControllingConditions.Any(c => !c.InitiallyActive && c.IsConstant));
                
                // Remove all action groups up until the last one where we're always on
                var lastAlwaysOnGroup = group.actionGroups.FindLastIndex(ag => ag.IsConstantOn);
                if (lastAlwaysOnGroup > 0)
                    group.actionGroups.RemoveRange(0, lastAlwaysOnGroup - 1);
            }

            // Remove shapes with no action groups
            foreach (var kvp in shapes.ToList())
                if (kvp.Value.actionGroups.Count == 0)
                    shapes.Remove(kvp.Key);
        }

        private void ProcessInitialAnimatorVariables(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            foreach (var group in shapes.Values)
            foreach (var agk in group.actionGroups)
            foreach (var condition in agk.ControllingConditions)
            {
                if (condition.IsConstant) continue;

                if (!initialValues.ContainsKey(condition.Parameter))
                    initialValues[condition.Parameter] = condition.InitialValue;
            }
        }

        private void PreprocessShapes(Dictionary<TargetProp, AnimatedProperty> shapes, out Dictionary<TargetProp, object> initialStates, out HashSet<TargetProp> deletedShapes)
        {
            // For each shapekey, determine 1) if we can just set an initial state and skip and 2) if we can delete the
            // corresponding mesh. If we can't, delete ops are merged into the main list of operations.
            
            initialStates = new Dictionary<TargetProp, object>();
            deletedShapes = new HashSet<TargetProp>();

            foreach (var (key, info) in shapes.ToList())
            {
                if (info.actionGroups.Count == 0)
                {
                    // never active control; ignore it entirely
                    shapes.Remove(key);
                    continue;
                }
                
                var deletions = info.actionGroups.Where(agk => agk.IsDelete).ToList();
                if (deletions.Any(d => d.ControllingConditions.All(c => c.IsConstantActive)))
                {
                    // always deleted
                    shapes.Remove(key);
                    deletedShapes.Add(key);
                    continue;
                }
                
                // Move deleted shapes to the end of the list, so they override all Set actions
                info.actionGroups = info.actionGroups.Where(agk => !agk.IsDelete).Concat(deletions).ToList();

                var initialState = info.actionGroups.Where(agk => agk.InitiallyActive)
                    .Select(agk => key.IsObjectReference ? agk.ObjectValue : (object) agk.Value)
                    .Prepend(info.currentState) // use scene state if everything is disabled
                    .Last();

                initialStates[key] = initialState;
                
                // If we're now constant-on, we can skip animation generation
                if (info.actionGroups[^1].IsConstant)
                {
                    shapes.Remove(key);
                }
            }
        }

        private void ResolveToggleInitialStates(Dictionary<TargetProp, AnimatedProperty> groups)
        {
            var asc = context.Extension<AnimationServicesContext>();
            
            Dictionary<string, bool> propStates = new Dictionary<string, bool>();
            Dictionary<string, bool> nextPropStates = new Dictionary<string, bool>();
            int loopLimit = 5;

            bool unsettled = true;
            while (unsettled && loopLimit-- > 0)
            {
                unsettled = false;

                foreach (var group in groups.Values)
                {
                    if (group.TargetProp.PropertyName != "m_IsActive") continue;
                    if (!(group.TargetProp.TargetObject is GameObject targetObject)) continue;

                    var pathKey = asc.GetActiveSelfProxy(targetObject);

                    bool state;
                    if (!propStates.TryGetValue(pathKey, out state)) state = targetObject.activeSelf;

                    foreach (var actionGroup in group.actionGroups)
                    {
                        bool evaluated = true;
                        foreach (var condition in actionGroup.ControllingConditions)
                        {
                            if (!propStates.TryGetValue(condition.Parameter, out var propCondition))
                            {
                                propCondition = condition.InitiallyActive;
                            }

                            if (!propCondition)
                            {
                                evaluated = false;
                                break;
                            }
                        }

                        if (evaluated)
                        {
                            state = actionGroup.Value > 0.5f;
                        }
                    }

                    nextPropStates[pathKey] = state;

                    if (!propStates.TryGetValue(pathKey, out var oldState) || oldState != state)
                    {
                        unsettled = true;
                    }
                }
                
                propStates = nextPropStates;
                nextPropStates = new();
            }

            foreach (var group in groups.Values)
            {
                foreach (var action in group.actionGroups)
                {
                    foreach (var condition in action.ControllingConditions)
                    {
                        if (propStates.TryGetValue(condition.Parameter, out var state))
                            condition.InitialValue = state ? 1.0f : 0.0f;
                    }
                }
            }
        }

        private void ProcessInitialStates(Dictionary<TargetProp, object> initialStates)
        {
            var asc = context.Extension<AnimationServicesContext>();
            
            // We need to track _two_ initial states: the initial state we'll apply at build time (which applies
            // when animations are disabled) and the animation base state. Confusingly, the animation base state
            // should be the state that is currently applied to the object...
            
            var clips = context.Extension<AnimationServicesContext>().AnimationDatabase;
            var initialStateHolder = clips.ClipsForPath(ReactiveObjectPrepass.TAG_PATH).FirstOrDefault();
            if (initialStateHolder == null) return;

            _initialStateClip = new AnimationClip();
            _initialStateClip.name = "MA Shape Changer Defaults";
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
                                animBaseState = prop.boolValue ? 1 : 0;
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
            if (info.alwaysDeleted) return;
            if (info.actionGroups[^1].IsConstant)
            {
                info.TargetProp.ApplyImmediate(info.actionGroups[0].Value);
                
                return;
            }

            var asm = GenerateStateMachine(info);
            ApplyController(asm, "MA Responsive: " + info.TargetProp.TargetObject.name);
        }

        private AnimatorStateMachine GenerateStateMachine(AnimatedProperty info)
        {
            var asc = context.Extension<AnimationServicesContext>();
            var asm = new AnimatorStateMachine();
            asm.name = "MA Shape Changer " + info.TargetProp.TargetObject.name;

            var x = 200;
            var y = 0;
            var yInc = 60;

            asm.anyStatePosition = new Vector3(-200, 0);

            var initial = new AnimationClip();
            var initialState = new AnimatorState();
            initialState.motion = initial;
            initialState.writeDefaultValues = false;
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

                var clip = AnimResult(group.TargetProp, group.TargetProp.IsObjectReference ? group.ObjectValue : group.Value);

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

                    var state = new AnimatorState();
                    state.name = group.ControllingConditions[0].DebugName;
                    state.motion = clip;
                    state.writeDefaultValues = false;
                    states.Add(new ChildAnimatorState
                    {
                        position = new Vector3(x, y),
                        state = state
                    });
                    asc.AnimationDatabase.RegisterState(states[^1].state);

                    var transitionList = new List<AnimatorStateTransition>();
                    transitionBuffer.Add((state, transitionList));
                    entryTransitions.Add(new AnimatorTransition
                    {
                        destinationState = state,
                        conditions = conditions
                    });

                    foreach (var cond in conditions)
                    {
                        var inverted = new AnimatorCondition
                        {
                            parameter = cond.parameter,
                            mode = cond.mode == AnimatorConditionMode.Greater
                                ? AnimatorConditionMode.Less
                                : AnimatorConditionMode.Greater,
                            threshold = cond.threshold
                        };
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
            }

            foreach (var (st, transitions) in transitionBuffer) st.transitions = transitions.ToArray();

            asm.states = states.ToArray();
            entryTransitions.Reverse();
            asm.entryTransitions = entryTransitions.ToArray();
            asm.exitPosition = new Vector3(500, 0);

            return asm;
        }

        private AnimatorCondition[] GetTransitionConditions(AnimationServicesContext asc, ReactionData group)
        {
            var conditions = new List<AnimatorCondition>();

            foreach (var condition in group.ControllingConditions)
            {
                if (condition.IsConstant) continue;

                conditions.Add(new AnimatorCondition
                {
                    parameter = condition.Parameter,
                    mode = AnimatorConditionMode.Greater,
                    threshold = condition.ParameterValueLo
                });

                conditions.Add(new AnimatorCondition
                {
                    parameter = condition.Parameter,
                    mode = AnimatorConditionMode.Less,
                    threshold = condition.ParameterValueHi
                });
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
            else if (key.TargetObject is SkinnedMeshRenderer smr)
            {
                path = RuntimeUtil.RelativePath(context.AvatarRootObject, smr.gameObject);
                componentType = typeof(SkinnedMeshRenderer);
            }
            else
            {
                throw new InvalidOperationException("Invalid target object: " + key.TargetObject);
            }

            var clip = new AnimationClip();
            clip.name = $"Set {path}:{key.PropertyName}={value}";

            if (key.IsObjectReference)
            {
                var binding = EditorCurveBinding.PPtrCurve(path, componentType, key.PropertyName);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new []
                {
                    new ObjectReferenceKeyframe()
                    {
                        value = (Object) value,
                        time = 0
                    },
                    new ObjectReferenceKeyframe()
                    {
                        value = (Object) value,
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

                if (key.TargetObject is GameObject obj && key.PropertyName == "m_IsActive")
                {
                    var asc = context.Extension<AnimationServicesContext>();
                    var propName = asc.GetActiveSelfProxy(obj);
                    binding = EditorCurveBinding.FloatCurve("", typeof(Animator), propName);
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }

            return clip;
        }

        private void ApplyController(AnimatorStateMachine asm, string layerName)
        {
            var fx = context.AvatarDescriptor.baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
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
                    name = "MA Shape Changer " + layerName,
                    defaultWeight = 1
                }
            ).ToArray();
        }
    }
}