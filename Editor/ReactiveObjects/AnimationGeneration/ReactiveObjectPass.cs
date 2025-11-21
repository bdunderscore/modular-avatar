#region

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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
            // Having a WD OFF layer after WD ON layers can break WD. We match the behavior of the existing states,
            // and if mixed, use WD ON to maximize compatibility.
            var asc = context.Extension<AnimatorServicesContext>();
#if MA_VRCSDK3_AVATARS
            if (!context.AvatarDescriptor) return;

            var fxLayer = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            if (fxLayer != null)
            {
                _writeDefaults = MergeAnimatorProcessor.AnalyzeLayerWriteDefaults(fxLayer) ?? true;
            }
            
            var clips = asc.AnimationIndex;
            _initialStateClip = clips.GetClipsForObjectPath(ReactiveObjectPrepass.TAG_PATH).FirstOrDefault();
#endif

            var analysis = new ReactiveObjectAnalyzer(context).Analyze(context.AvatarRootObject);

            var shapes = analysis.Shapes;
            var initialStates = analysis.InitialStates;
            
            // XXX: Until we have a proper API for reactive components, on non-VRChat platforms we simply force everything
            // to be constant-state.
            var generateAnimations = context.PlatformProvider.QualifiedName == WellKnownPlatforms.VRChatAvatar30;
            if (!generateAnimations)
            {
                foreach (var (target, animProp) in shapes)
                {
                    var lastAction = animProp.actionGroups.LastOrDefault(ag => ag.InitiallyActive);
                    if (lastAction != null)
                    {
                        lastAction.ControllingConditions.Clear();
                        animProp.actionGroups = new List<ReactionRule>() { lastAction };
                    }
                    else
                    {
                        animProp.actionGroups.Clear();
                    }
                }
            }
            
            GenerateActiveSelfProxies(shapes);

            RemoveRedundantStaticStates(shapes);
            ProcessMeshDeletion(initialStates, shapes);

            ProcessInitialStates(initialStates, shapes);
            ProcessInitialAnimatorVariables(shapes);
            
#if MA_VRCSDK3_AVATARS
            if (generateAnimations)
            {
                GenerateParameters(shapes);
                
                foreach (var groups in shapes.Values)
                {
                    ProcessShapeKey(groups);
                }
            }
#endif
        }

        private void RemoveRedundantStaticStates(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var constantShapes = shapes
                .Where(kv => kv.Value.actionGroups.LastOrDefault()?.IsConstant is true)
                .Where(kv => kv.Value.actionGroups.All(x => x.Value is not IVertexFilter))
                .Where(kv => kv.Value.overrideStaticState == null)
                .ToList();

            var asc = context.Extension<AnimatorServicesContext>();

            foreach (var (k, v) in constantShapes)
            {
                // If there are animations targeting this property already, we need to keep the animation generation
                // to ensure we override the existing animations correctly.

                GameObject targetGameObject;
                switch (k.TargetObject)
                {
                    case GameObject go: targetGameObject = go; break;
                    case Behaviour b: targetGameObject = b.gameObject; break;
                    case Component c: targetGameObject = c.gameObject; break;
                    default: continue;
                }

                var ecb = EditorCurveBinding.FloatCurve(
                    asc.ObjectPathRemapper.GetVirtualPathForObject(targetGameObject),
                    k.TargetObject.GetType(),
                    k.PropertyName
                );

                if (asc.AnimationIndex.GetClipsForBinding(ecb).Any(clip => clip != _initialStateClip)) continue;

                // If there are no animations targeting this property, we can skip animation generation
                shapes.Remove(k);
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

            // On non-vrc builds, we don't run the prepass, but we still need to apply the initial states,
            // so just create a clip that we'll throw away later.
            if (_initialStateClip == null) _initialStateClip = VirtualClip.Create("Dummy clip");

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

        private IVertexFilter AggregateVertexFilters(IEnumerable<IVertexFilter> filters)
        {
            filters = filters.ToList();
            var filter = filters.LastOrDefault(f => f != null);
            if (filter is VertexFilterByShape filterByShape)
            {
                return new VertexFilterByShape(filterByShape.Shapes, filters
                    .OfType<VertexFilterByShape>()
                    .Min(x => x.Threshold));
            }
            return filter;
        }
        
        private void ProcessMeshDeletion(Dictionary<TargetProp, object> initialStates,
            Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var renderers = shapes.Values
                .Where(prop => prop.actionGroups.Any(x => x.Value is IVertexFilter))
                .GroupBy(prop => prop.TargetProp.TargetObject as SkinnedMeshRenderer)
                .ToList();
            foreach (var grouping in renderers)
            {
                var renderer = grouping.Key;
                var toDelete = grouping
                    .Where(prop =>
                    {
                        var activeGroup = prop.actionGroups.LastOrDefault();
                        return activeGroup?.IsConstantActive is true && activeGroup?.Value is IVertexFilter;
                    })
                    .Select(prop => (
                        prop.TargetProp,
                        VertexFilter: AggregateVertexFilters(prop.actionGroups.Select(x => x.Value as IVertexFilter))
                    ))
                    .ToList();
                var toNaNimate = grouping
                    .Where(prop => prop.actionGroups.LastOrDefault()?.IsConstantActive is false)
                    .Select(prop => (
                        prop.TargetProp,
                        VertexFilter: AggregateVertexFilters(prop.actionGroups.Select(x => x.Value as IVertexFilter))
                    ))
                    .ToList();
                
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                renderer.sharedMesh = mesh = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, toDelete);

                foreach (var (prop, _) in toDelete)
                {
                    // Don't need to animate this anymore...!
                    shapes.Remove(prop);
                    initialStates.Remove(prop);

                    if (prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedShapePrefix))
                    {
                        var shapeName = prop.PropertyName[ReactiveObjectAnalyzer.DeletedShapePrefix.Length..];
                        var shapeProp = new TargetProp
                        {
                            TargetObject = renderer,
                            PropertyName = ReactiveObjectAnalyzer.BlendshapePrefix + shapeName
                        };
                        shapes.Remove(shapeProp);
                        initialStates.Remove(shapeProp);
                    }
                }

                if (toNaNimate.Count == 0)
                {
                    continue;
                }

                // Handle NaNimated shapes next
                var nanPlan = NaNimationFilter.ComputeNaNPlan(renderer, ref mesh, toNaNimate);
                renderer.sharedMesh = mesh;
                
                if (nanPlan.Count > 0)
                {
                    var nanBones = GenerateNaNimatedBones(renderer, nanPlan);

                    HashSet<GameObject> initiallyActive = new HashSet<GameObject>();
                    
                    foreach (var kv in nanBones)
                    {
                        (var targetProp, var filter) = kv.Key;
                        var bones = kv.Value;
                        
                        var animProp = shapes[targetProp];

                        var clip_delete = CreateNaNimationClip(renderer, filter.ToString(), bones, true);
                        var clip_retain = CreateNaNimationClip(renderer, filter.ToString(), bones, false);

                        foreach (var group in animProp.actionGroups)
                        {
                            group.CustomApplyMotion = group.Value is IVertexFilter ? clip_delete : clip_retain;
                        }

                        var isInitiallyActive = animProp.actionGroups.Any(agk => agk.InitiallyActive);
                        if (isInitiallyActive)
                        {
                            initiallyActive.UnionWith(bones);
                        }

                        // Since we won't be inserting this into the default states animation, make sure there's a default
                        // motion to fall back on for non-WD setups.
                        animProp.actionGroups.Insert(0, new ReactionRule(targetProp, 0.0f)
                        {
                            CustomApplyMotion = clip_retain
                        });
                    }

#if MA_VRCSDK3_AVATARS
                    NaNimationInitialStateMunger.ApplyInitialStates(
                        context.AvatarRootTransform,
                        initiallyActive,
                        _initialStateClip
                    );
#endif
                }

                // Prevent generating unnecessary animator layers when VertexFilter ignores all vertices
                var nanimatedProps = nanPlan.Select(x => x.Key.Item1).ToHashSet();
                foreach (var (prop, _) in toNaNimate.Where(x => !nanimatedProps.Contains(x.TargetProp)))
                {
                    // Don't need to animate this anymore...!
                    shapes.Remove(prop);
                    initialStates.Remove(prop);

                    if (prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedShapePrefix))
                    {
                        var shapeName = prop.PropertyName[ReactiveObjectAnalyzer.DeletedShapePrefix.Length..];
                        var shapeProp = new TargetProp
                        {
                            TargetObject = renderer,
                            PropertyName = ReactiveObjectAnalyzer.BlendshapePrefix + shapeName
                        };
                        shapes.Remove(shapeProp);
                        initialStates.Remove(shapeProp);
                    }
                }
            }
        }

        private VirtualClip CreateNaNimationClip(SkinnedMeshRenderer renderer, string shapeName, List<GameObject> bones,
            bool shouldDelete)
        {
            var asc = context.Extension<AnimatorServicesContext>();

            var clip = VirtualClip.Create($"NaNimation for {shapeName} ({(shouldDelete ? "delete" : "retain")})");

            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0, 0)
            {
                value = shouldDelete ? float.NaN : 1.0f
            });

            foreach (var bone in bones)
            {
                var boneName = asc.ObjectPathRemapper
                    .GetVirtualPathForObject(bone);

                foreach (var dim in new[] { "x", "y", "z" })
                {
                    var binding = EditorCurveBinding.FloatCurve(
                        boneName,
                        typeof(Transform),
                        $"m_LocalScale.{dim}"
                    );
                    clip.SetFloatCurve(binding, curve);
                }

                if (shouldDelete)
                {
                    // AABB recalculation will cause a ton of warnings due to invalid vertex coordinates, so disable it
                    // when any NaNimation is active.
                    clip.SetFloatCurve(
                        asc.ObjectPathRemapper.GetVirtualPathForObject(renderer.gameObject),
                        typeof(SkinnedMeshRenderer),
                        "m_UpdateWhenOffscreen",
                        AnimationCurve.Constant(0, 1, 0)
                    );
                }
            }

            return clip;
        }

        private Dictionary<(TargetProp, IVertexFilter), List<GameObject>> GenerateNaNimatedBones(SkinnedMeshRenderer renderer,
            Dictionary<(TargetProp, IVertexFilter), List<NaNimationFilter.AddedBone>> plan)
        {
            List<(NaNimationFilter.AddedBone, (TargetProp, IVertexFilter))> createdBones =
                plan.SelectMany(kv => kv.Value.Select(bone => (bone, kv.Key)))
                    .OrderBy(b => b.bone.newBoneIndex)
                    .ToList();

            var maxNewBoneIndex = createdBones[^1].Item1.newBoneIndex;
            var bonesArray = new Transform[maxNewBoneIndex + 1];
            var curBonesArray = renderer.bones;
            Array.Copy(curBonesArray, 0, bonesArray, 0, curBonesArray.Length);

            // Special case for meshes with no bones; we need to create a bone 0 
            if (curBonesArray.Length == 0)
            {
                bonesArray[0] = renderer.transform;
            }

            foreach (var pair in createdBones)
            {
                var bone = pair.Item1;
                var shape = pair.Item2;

                if (bonesArray[bone.originalBoneIndex] == null) continue;
                var newBone = new GameObject(NaNimationFilter.NaNimatedBonePrefix + shape.Item1.ToString().Replace('/', '_'));
                var newBoneTransform = newBone.transform;
                newBoneTransform.SetParent(bonesArray[bone.originalBoneIndex], false);
                newBoneTransform.localPosition = Vector3.zero;
                newBoneTransform.localRotation = Quaternion.identity;
                newBoneTransform.localScale = Vector3.one;

                newBone.AddComponent<ModularAvatarPBBlocker>();
                bonesArray[bone.newBoneIndex] = newBoneTransform;
            }

            renderer.bones = bonesArray;

            return plan.ToDictionary(kv => kv.Key,
                kv => kv.Value.Select(b => bonesArray[b.newBoneIndex].gameObject).ToList());
        }

        #endregion

#if MA_VRCSDK3_AVATARS
        private void GenerateParameters(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var usedParams = new HashSet<string>();

            foreach (var prop in shapes.Values)
            {
                foreach (var group in prop.actionGroups)
                {
                    foreach (var cond in group.ControllingConditions)
                    {
                        if (!string.IsNullOrEmpty(cond.Parameter))
                        {
                            usedParams.Add(cond.Parameter);
                        }
                    }
                }
            }

            var asc = context.Extension<AnimatorServicesContext>();
            if (!asc.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fx))
            {
                return;
            }

            var parameters = fx.Parameters;
            foreach (var usedParam in usedParams)
            {
                if (parameters.TryGetValue(usedParam, out var p) && p.type == AnimatorControllerParameterType.Float)
                {
                    continue;
                }

                if (p == null)
                {
                    p = new AnimatorControllerParameter
                    {
                        name = usedParam,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = 0.0f
                    };
                }
                else
                {
                    p = new AnimatorControllerParameter
                    {
                        type = AnimatorControllerParameterType.Float,
                        name = p.name,
                        defaultFloat = p.type switch
                        {
                            AnimatorControllerParameterType.Bool => p.defaultBool ? 1 : 0,
                            AnimatorControllerParameterType.Int => p.defaultInt,
                            _ => 0
                        }
                    };
                }

                parameters = parameters.SetItem(usedParam, p);
            }

            fx.Parameters = parameters;
        }
        
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
#endif

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
            else if (value is float valueFloat)
            {
                var curve = new AnimationCurve();
                curve.AddKey(0, (float) valueFloat);
                curve.AddKey(1, (float) valueFloat);

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
    }
}