#if MA_VRCSDK3_AVATARS
#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            var fxLayer = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            if (fxLayer != null)
            {
                _writeDefaults = MergeAnimatorProcessor.AnalyzeLayerWriteDefaults(fxLayer) ?? true;
            }
            
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

        private bool? GetConstantStateForTargetProp(
            TargetProp prop,
            Dictionary<TargetProp, object> initialStates,
            Dictionary<TargetProp, AnimatedProperty> shapes
        )
        {
            if (!shapes.ContainsKey(prop))
            {
                return initialStates.GetValueOrDefault(prop) as bool?;
            }

            var lastGroup = shapes[prop].actionGroups.LastOrDefault();
            if (lastGroup?.IsConstantActive != true) return null;

            return lastGroup.Value is float;
        }
        
        private void ProcessMeshDeletion(Dictionary<TargetProp, object> initialStates,
            Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var renderers = initialStates.Keys
                .Where(prop => prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedShapePrefix))
                .Where(prop => prop.TargetObject is SkinnedMeshRenderer)
                .Where(prop => GetConstantStateForTargetProp(prop, initialStates, shapes) != false)
                .GroupBy(prop => prop.TargetObject as SkinnedMeshRenderer)
                .ToList();
            foreach (var grouping in renderers)
            {
                var renderer = grouping.Key;
                var shapeNamesToDelete = grouping
                    .Where(prop => GetConstantStateForTargetProp(prop, initialStates, shapes) == true)
                    .Select(prop => (
                        prop.PropertyName.Substring(ReactiveObjectAnalyzer.DeletedShapePrefix.Length),
                        shapes[prop].actionGroups.Select(ag => ag.Value).OfType<float>().Min()
                    ))
                    .ToList();
                
                var nanimatedShapes = grouping
                    .Where(prop => GetConstantStateForTargetProp(prop, initialStates, shapes) != true)
                    .Where(shapes.ContainsKey)
                    .Select(prop => (
                            prop.PropertyName.Substring(ReactiveObjectAnalyzer.DeletedShapePrefix.Length),
                            shapes[prop].actionGroups.Select(ag => ag.Value).OfType<float>().Min()
                    ))
                    .ToList();
                
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var shapesToDelete = shapeNamesToDelete
                    .Select(kv => (mesh.GetBlendShapeIndex(kv.Item1), kv.Item2))
                    .Where(kv => kv.Item1 >= 0)
                    .ToList();

                renderer.sharedMesh = mesh = RemoveBlendShapeFromMesh.RemoveBlendshapes(mesh, shapesToDelete);

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

                var clips = ConstraintVertexHider.GenerateConstrainthider(context, renderer, ref mesh, nanimatedShapes);
                renderer.sharedMesh = mesh;

                // Handle NaNimated shapes next
                /*
                var nanPlan = NaNimationFilter.ComputeNaNPlan(ref mesh, nanimatedShapes, renderer.bones.Length);
                renderer.sharedMesh = mesh;

                if (nanPlan.Count > 0)
                {
                    foreach (var kv in nanPlan)
                    {
                        var shapeName = kv.Key;
                        var newShape = kv.Value;

                        var deleteTarget = new TargetProp
                        {
                            TargetObject = renderer,
                            PropertyName = ReactiveObjectAnalyzer.DeletedShapePrefix + shapeName
                        };
                        var animProp = shapes[deleteTarget];

                        var clip_delete = CreateNaNimationClip(renderer, shapeName, newShape, true);
                        var clip_retain = CreateNaNimationClip(renderer, shapeName, newShape, false);

                        foreach (var group in animProp.actionGroups)
                        {
                            var isDeleted = group.Value is float;

                            group.CustomApplyMotion = isDeleted ? clip_delete : clip_retain;
                        }

                        var index = renderer.sharedMesh.GetBlendShapeIndex(newShape);
                        var initialWeight = animProp.actionGroups.Any(ag => ag.InitiallyActive) ? 1.0f : 0.0f;
                        renderer.SetBlendShapeWeight(index, initialWeight);
                        renderer.updateWhenOffscreen = false;

                        // Since we won't be inserting this into the default states animation, make sure there's a default
                        // motion to fall back on for non-WD setups.
                        animProp.actionGroups.Insert(0, new ReactionRule(deleteTarget, 0.0f)
                        {
                            CustomApplyMotion = clip_retain
                        });
                    }
                }*/
            }
        }

        private VirtualClip CreateNaNimationClip(SkinnedMeshRenderer renderer, string shapeName, string nanShape,
            bool shouldDelete)
        {
            var asc = context.Extension<AnimatorServicesContext>();

            var clip = VirtualClip.Create($"NaNimation for {shapeName} ({(shouldDelete ? "delete" : "retain")})");

            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0, 0)
            {
                value = shouldDelete ? 1.0f : 0.0f
            });

            var binding = EditorCurveBinding.FloatCurve(
                RuntimeUtil.AvatarRootPath(renderer.gameObject),
                typeof(SkinnedMeshRenderer),
                ReactiveObjectAnalyzer.BlendshapePrefix + nanShape
            );
            clip.SetFloatCurve(binding, curve);

            // AABB recalculation will cause a ton of warnings due to invalid vertex coordinates, so disable it
            // when any NaNimation is present.
            clip.SetFloatCurve(
                asc.ObjectPathRemapper.GetVirtualPathForObject(renderer.gameObject),
                typeof(SkinnedMeshRenderer),
                "m_UpdateWhenOffscreen",
                AnimationCurve.Constant(0, 1, 0)
            );
            
            return clip;
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
            GenerateStateMachine(info);
        }

        private void GenerateStateMachine(AnimatedProperty info)
        {
            var asc = context.Extension<AnimatorServicesContext>();
            var asm = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX]!
                .AddLayer(LayerPriority.Default, $"MA Responsive: {info.TargetProp.TargetObject.name}").StateMachine!;

            var x = 200;
            var y = 0;
            var yInc = 60;

            asm.AnyStatePosition = new Vector3(-200, 0);

            var initialState = asm.AddState("<default>");
            initialState.WriteDefaultValues = _writeDefaults;
            asm.DefaultState = initialState;

            asm.EntryPosition = new Vector3(0, 0);

            var lastConstant = info.actionGroups.FindLastIndex(agk => agk.IsConstant);
            var transitionBuffer = new List<(VirtualState, List<VirtualStateTransition>)>();
            var entryTransitions = new List<VirtualTransition>();

            transitionBuffer.Add((initialState, new List<VirtualStateTransition>()));

            // Note: We need to generate a group for any base constant state as well; this is because we generate the
            // scene initial value as a base animation curve in the base blend tree, which would be exposed in the
            // default state. This is incorrect when there is a constant-on Object Toggle or similar changing the
            // initial state of a property.
            //
            // We can, however, skip any groups _before_ that constant state, as they'll be overridden in all cases.
            foreach (var group in info.actionGroups.Skip(Math.Max(0, lastConstant - 1)))
            {
                y += yInc;

                // TODO - avoid clone
                var clip = group.CustomApplyMotion ??
                           asc.ControllerContext.Clone(AnimResult(group.TargetProp, group.Value));

                if (group.IsConstant)
                {
                    clip.Name = "Property Overlay constant " + group.Value;
                    initialState.Motion = clip;
                }
                else
                {
                    clip.Name = "Property Overlay controlled by " + group.ControllingConditions[0].DebugName + " " +
                                group.Value;

                    var conditions = GetTransitionConditions(group);

                    foreach (var (st, transitions) in transitionBuffer)
                    {
                        if (!group.Inverted)
                        {
                            var transition = VirtualStateTransition.Create();
                            transition.SetExitDestination();
                            transition.ExitTime = null;
                            transition.Duration = 0;
                            transition.HasFixedDuration = true;
                            transition.Conditions = conditions.ToImmutableList();
                            transitions.Add(transition);
                        }
                        else
                        {
                            foreach (var cond in conditions)
                            {
                                var transition = VirtualStateTransition.Create();
                                transition.SetExitDestination();
                                transition.ExitTime = null;
                                transition.Duration = 0;
                                transition.HasFixedDuration = true;
                                transition.Conditions = new[] { InvertCondition(cond) }.ToImmutableList();
                                transitions.Add(transition);
                            }
                        }
                    }

                    // Workaround for the warning: "'.' is not allowed in State name"
                    var state = asm.AddState(
                        group.ControllingConditions[0].DebugName.Replace(".", "_"),
                        clip,
                        new Vector3(x, y)
                    );

                    state.WriteDefaultValues = _writeDefaults;

                    var transitionList = new List<VirtualStateTransition>();
                    transitionBuffer.Add((state, transitionList));

                    if (!group.Inverted)
                    {
                        var entryTransition = VirtualTransition.Create();
                        entryTransition.SetDestination(state);
                        entryTransition.Conditions = conditions.ToImmutableList();
                        entryTransitions.Add(entryTransition);

                        foreach (var cond in conditions)
                        {
                            var inverted = InvertCondition(cond);
                            var transition = VirtualStateTransition.Create();
                            transition.SetExitDestination();
                            transition.ExitTime = null;
                            transition.Duration = 0;
                            transition.HasFixedDuration = true;
                            transition.Conditions = new[] { inverted }.ToImmutableList();
                            transitionList.Add(transition);
                        }
                    }
                    else
                    {
                        // inverted condition
                        foreach (var cond in conditions)
                        {
                            var entryTransition = VirtualTransition.Create();
                            entryTransition.SetDestination(state);
                            entryTransition.Conditions = new[] { InvertCondition(cond) }.ToImmutableList();
                            entryTransitions.Add(entryTransition);
                        }

                        var transition = VirtualStateTransition.Create();
                        transition.SetExitDestination();
                        transition.ExitTime = null;
                        transition.Duration = 0;
                        transition.HasFixedDuration = true;
                        transition.Conditions = conditions.ToImmutableList();

                        transitionList.Add(transition);
                    }
                }
            }

            if (initialState.Motion == null)
            {
                // For some reason, if we set the state's motion multiple times, Unity will sometimes revert to the
                // first motion set; as such, make sure to set the empty motion only if we really mean it. 
                var initial = VirtualClip.Create("empty motion");
                initialState.Motion = initial;
            }

            foreach (var (st, transitions) in transitionBuffer) st.Transitions = transitions.ToImmutableList();

            entryTransitions.Reverse();
            asm.EntryTransitions = entryTransitions.ToImmutableList();
            asm.ExitPosition = new Vector3(500, 0);
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