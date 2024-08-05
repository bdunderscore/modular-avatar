#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using EditorCurveBinding = UnityEditor.EditorCurveBinding;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Reserve an animator layer for Shape Changer's use. We do this here so that we can take advantage of MergeAnimator's
    ///     layer reference correction logic; this can go away once we have a more unified animation services API.
    /// </summary>
    internal class PropertyOverlayPrePass : Pass<PropertyOverlayPrePass>
    {
        internal const string TAG_PATH = "__MA/ShapeChanger/PrepassPlaceholder";

        protected override void Execute(ndmf.BuildContext context)
        {
            var hasShapeChanger = context.AvatarRootObject.GetComponentInChildren<ModularAvatarShapeChanger>() != null;
            var hasObjectSwitcher =
                context.AvatarRootObject.GetComponentInChildren<ModularAvatarObjectToggle>() != null;
            if (hasShapeChanger || hasObjectSwitcher)
            {
                var clip = new AnimationClip();
                clip.name = "MA Shape Changer Defaults";

                var curve = new AnimationCurve();
                curve.AddKey(0, 0);
                clip.SetCurve(TAG_PATH, typeof(Transform), "localPosition.x", curve);

                // Merge using a null blend tree. This also ensures that we initialize the Merge Blend Tree system.
                var bt = new BlendTree();
                bt.name = "MA Shape Changer Defaults";
                bt.blendType = BlendTreeType.Direct;
                bt.children = new[]
                {
                    new ChildMotion
                    {
                        motion = clip,
                        timeScale = 1,
                        cycleOffset = 0,
                        directBlendParameter = MergeBlendTreePass.ALWAYS_ONE
                    }
                };
                bt.useAutomaticThresholds = false;

                // This is a hack and a half - put in a dummy path so we can find the cloned clip later on...
                var obj = new GameObject("MA SC Defaults");
                obj.transform.SetParent(context.AvatarRootTransform);
                var mambt = obj.AddComponent<ModularAvatarMergeBlendTree>();
                mambt.BlendTree = bt;
                mambt.PathMode = MergeAnimatorPathMode.Absolute;
            }
        }
    }
    
    internal class PropertyOverlayPass
    {
        struct TargetProp
        {
            public Object TargetObject;
            public string PropertyName;

            public bool Equals(TargetProp other)
            {
                return Equals(TargetObject, other.TargetObject) && PropertyName == other.PropertyName;
            }

            public override bool Equals(object obj)
            {
                return obj is TargetProp other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (TargetObject != null ? TargetObject.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (PropertyName != null ? PropertyName.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public void ApplyImmediate(float value)
            {
                var renderer = (SkinnedMeshRenderer)TargetObject;
                renderer.SetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(
                    PropertyName.Substring("blendShape.".Length)
                ), value);
            }
        }

        class PropGroup
        {
            public TargetProp TargetProp { get; }
            public string ControlParam { get; set; }

            public bool alwaysDeleted;
            public float currentState;

            // Objects which trigger deletion of this shape key. 
            public List<ActionGroupKey> actionGroups = new List<ActionGroupKey>();

            public PropGroup(TargetProp key, float currentState)
            {
                TargetProp = key;
                this.currentState = currentState;
            }
        }

        class ActionGroupKey
        {
            public ActionGroupKey(ndmf.BuildContext context, TargetProp key, GameObject controllingObject, float value)
            {
                var asc = context.Extension<AnimationServicesContext>();
                
                TargetProp = key;

                var conditions = new List<ControlCondition>();

                var cursor = controllingObject?.transform;

                while (cursor != null && !RuntimeUtil.IsAvatarRoot(cursor))
                {
                    if (asc.TryGetActiveSelfProxy(cursor.gameObject, out var paramName))
                        conditions.Add(new ControlCondition
                        {
                            Parameter = paramName,
                            DebugName = cursor.gameObject.name,
                            IsConstant = false,
                            InitialValue = cursor.gameObject.activeSelf ? 1.0f : 0.0f,
                            ParameterValueLo = 0.5f,
                            ParameterValueHi = 1.5f
                        });
                    else if (!cursor.gameObject.activeSelf)
                        conditions = new List<ControlCondition>
                        {
                            new ControlCondition
                            {
                                Parameter = "",
                                DebugName = cursor.gameObject.name,
                                IsConstant = true,
                                InitialValue = 0,
                                ParameterValueLo = 0.5f,
                                ParameterValueHi = 1.5f
                            }
                        };

                    foreach (var mami in cursor.GetComponents<ModularAvatarMenuItem>())
                        conditions.Add(ParameterAssignerPass.AssignMenuItemParameter(context, mami));

                    cursor = cursor.parent;
                }

                ControllingConditions = conditions;
                
                Value = value;
            }

            public TargetProp TargetProp;
            public float Value;

            public readonly List<ControlCondition> ControllingConditions;

            public bool InitiallyActive =>
                ControllingConditions.Count == 0 || ControllingConditions.All(c => c.InitiallyActive);
            public bool IsDelete;

            public bool IsConstant => ControllingConditions.Count == 0 || ControllingConditions.All(c => c.IsConstant);

            public override string ToString()
            {
                return $"AGK: {TargetProp}={Value}";
            }

            public bool TryMerge(ActionGroupKey other)
            {
                if (!TargetProp.Equals(other.TargetProp)) return false;
                if (Mathf.Abs(Value - other.Value) > 0.001f) return false;
                if (!ControllingConditions.SequenceEqual(other.ControllingConditions)) return false;
                if (IsDelete || other.IsDelete) return false;

                return true;
            }
        }

        private readonly ndmf.BuildContext context;
        private Dictionary<string, float> initialValues = new();

        private AnimationClip _initialStateClip;
        
        public PropertyOverlayPass(ndmf.BuildContext context)
        {
            this.context = context;
        }

        internal void Execute()
        {
            Dictionary<TargetProp, PropGroup> shapes = FindShapes(context);
            FindObjectToggles(shapes, context);
            
            PreprocessShapes(shapes, out var initialStates, out var deletedShapes);
            
            ProcessInitialStates(initialStates);
            ProcessInitialAnimatorVariables(shapes);
            
            foreach (var groups in shapes.Values)
            {
                ProcessShapeKey(groups);
            }

            ProcessMeshDeletion(deletedShapes);
        }

        private void ProcessInitialAnimatorVariables(Dictionary<TargetProp, PropGroup> shapes)
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

        private void PreprocessShapes(Dictionary<TargetProp, PropGroup> shapes, out Dictionary<TargetProp, float> initialStates, out HashSet<TargetProp> deletedShapes)
        {
            // For each shapekey, determine 1) if we can just set an initial state and skip and 2) if we can delete the
            // corresponding mesh. If we can't, delete ops are merged into the main list of operations.
            
            initialStates = new Dictionary<TargetProp, float>();
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
                if (deletions.Any(d => d.ControllingConditions.Count == 0))
                {
                    // always deleted
                    shapes.Remove(key);
                    deletedShapes.Add(key);
                    continue;
                }
                
                // Move deleted shapes to the end of the list, so they override all Set actions
                info.actionGroups = info.actionGroups.Where(agk => !agk.IsDelete).Concat(deletions).ToList();

                var initialState = info.actionGroups.Where(agk => agk.InitiallyActive)
                    .Select(agk => agk.Value)
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

        private void ProcessInitialStates(Dictionary<TargetProp, float> initialStates)
        {
            // We need to track _two_ initial states: the initial state we'll apply at build time (which applies
            // when animations are disabled) and the animation base state. Confusingly, the animation base state
            // should be the state that is currently applied to the object...
            
            var clips = context.Extension<AnimationServicesContext>().AnimationDatabase;
            var initialStateHolder = clips.ClipsForPath(PropertyOverlayPrePass.TAG_PATH).FirstOrDefault();
            if (initialStateHolder == null) return;

            _initialStateClip = new AnimationClip();
            _initialStateClip.name = "MA Shape Changer Defaults";
            initialStateHolder.CurrentClip = _initialStateClip;

            foreach (var (key, initialState) in initialStates)
            {
                string path;
                Type componentType;

                var applied = false;
                float animBaseState = 0;
                
                if (key.TargetObject is GameObject go)
                {
                    path = RuntimeUtil.RelativePath(context.AvatarRootObject, go);
                    componentType = typeof(GameObject);
                }
                else if (key.TargetObject is SkinnedMeshRenderer smr)
                {
                    path = RuntimeUtil.RelativePath(context.AvatarRootObject, smr.gameObject);
                    componentType = typeof(SkinnedMeshRenderer);

                    if (key.PropertyName.StartsWith("blendShape."))
                    {
                        var blendShape = key.PropertyName.Substring("blendShape.".Length);
                        var index = smr.sharedMesh?.GetBlendShapeIndex(blendShape);

                        if (index != null && index >= 0)
                        {
                            animBaseState = smr.GetBlendShapeWeight(index.Value);
                            smr.SetBlendShapeWeight(index.Value, initialState);
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
                        switch (prop.propertyType)
                        {
                            case SerializedPropertyType.Boolean:
                                animBaseState = prop.boolValue ? 1 : 0;
                                prop.boolValue = initialState > 0.5f;
                                break;
                            case SerializedPropertyType.Float:
                                animBaseState = prop.floatValue;
                                prop.floatValue = initialState;
                                break;
                        }
                }

                var curve = new AnimationCurve();
                curve.AddKey(0, animBaseState);
                curve.AddKey(1, animBaseState);
                
                var binding = EditorCurveBinding.FloatCurve(
                    path,
                    componentType,
                    key.PropertyName
                );

                AnimationUtility.SetEditorCurve(_initialStateClip, binding, curve);
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

        private void ProcessShapeKey(PropGroup info)
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

        private AnimatorStateMachine GenerateStateMachine(PropGroup info)
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

        private AnimatorCondition[] GetTransitionConditions(AnimationServicesContext asc, ActionGroupKey group)
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

        private Motion AnimResult(TargetProp key, float value)
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

            var curve = new AnimationCurve();
            curve.AddKey(0, value);
            curve.AddKey(1, value);

            var binding = EditorCurveBinding.FloatCurve(path, componentType, key.PropertyName);
            AnimationUtility.SetEditorCurve(clip, binding, curve);

            if (key.TargetObject is GameObject obj && key.PropertyName == "m_IsActive")
            {
                var asc = context.Extension<AnimationServicesContext>();
                if (asc.TryGetActiveSelfProxy(obj, out var propName))
                {
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

        private AnimationClip AnimParam(string param, float val)
        {
            return AnimParam((param, val));
        }

        private AnimationClip AnimParam(params (string param, float val)[] pairs)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = "Set " + string.Join(", ", pairs.Select(p => $"{p.param}={p.val}"));

            // TODO - check property syntax
            foreach (var (param, val) in pairs)
            {
                var curve = new AnimationCurve();
                curve.AddKey(0, val);
                curve.AddKey(1, val);
                clip.SetCurve("", typeof(Animator), "" + param, curve);
            }

            return clip;
        }

        private void FindObjectToggles(Dictionary<TargetProp, PropGroup> objectGroups, ndmf.BuildContext context)
        {
            var asc = context.Extension<AnimationServicesContext>();

            var toggles = this.context.AvatarRootObject.GetComponentsInChildren<ModularAvatarObjectToggle>(true);

            foreach (var toggle in toggles)
            {
                if (toggle.Objects == null) continue;

                foreach (var obj in toggle.Objects)
                {
                    var target = obj.Object.Get(toggle);
                    if (target == null) continue;

                    // Make sure we generate an animator prop for each controlled object, as we intend to generate
                    // animations for them.
                    asc.ForceGetActiveSelfProxy(target);

                    var key = new TargetProp
                    {
                        TargetObject = target,
                        PropertyName = "m_IsActive"
                    };

                    if (!objectGroups.TryGetValue(key, out var group))
                    {
                        group = new PropGroup(key, target.activeSelf ? 1 : 0);
                        objectGroups[key] = group;
                    }

                    var value = obj.Active ? 1 : 0;
                    var action = new ActionGroupKey(context, key, toggle.gameObject, value);

                    if (action.IsConstant)
                    {
                        if (action.InitiallyActive)
                            // always active control
                            group.actionGroups.Clear();
                        else
                            // never active control
                            continue;
                    }

                    if (group.actionGroups.Count == 0)
                        group.actionGroups.Add(action);
                    else if (!group.actionGroups[^1].TryMerge(action)) group.actionGroups.Add(action);
                }
            }
        }
        
        private Dictionary<TargetProp, PropGroup> FindShapes(ndmf.BuildContext context)
        {
            var asc = context.Extension<AnimationServicesContext>();

            var changers = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarShapeChanger>(true);

            Dictionary<TargetProp, PropGroup> shapeKeys = new();

            foreach (var changer in changers)
            {
                var renderer = changer.targetRenderer.Get(changer)?.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) continue;

                int rendererInstanceId = renderer.GetInstanceID();
                var mesh = renderer.sharedMesh;

                if (mesh == null) continue;

                foreach (var shape in changer.Shapes)
                {
                    var shapeId = mesh.GetBlendShapeIndex(shape.ShapeName);
                    if (shapeId < 0) continue;

                    var key = new TargetProp
                    {
                        TargetObject = renderer,
                        PropertyName = "blendShape." + shape.ShapeName,
                    };

                    var value = shape.ChangeType == ShapeChangeType.Delete ? 100 : shape.Value;
                    if (!shapeKeys.TryGetValue(key, out var info))
                    {
                        info = new PropGroup(key, renderer.GetBlendShapeWeight(shapeId));
                        shapeKeys[key] = info;

                        // Add initial state
                        var agk = new ActionGroupKey(context, key, null, value);
                        agk.Value = renderer.GetBlendShapeWeight(shapeId);
                        info.actionGroups.Add(agk);
                    }

                    var action = new ActionGroupKey(context, key, changer.gameObject, value);
                    var isCurrentlyActive = changer.gameObject.activeInHierarchy;

                    if (shape.ChangeType == ShapeChangeType.Delete)
                    {
                        action.IsDelete = true;
                        
                        if (isCurrentlyActive) info.currentState = 100;

                        info.actionGroups.Add(action); // Never merge

                        continue;
                    }

                    if (changer.gameObject.activeInHierarchy) info.currentState = action.Value;

                    // TODO: lift controlling object resolution out of loop?
                    if (action.IsConstant)
                    {
                        if (action.InitiallyActive)
                        {
                            // always active control
                            info.actionGroups.Clear();
                        }
                        else
                        {
                            // never active control
                            continue;
                        }
                    }

                    Debug.Log("Trying merge: " + action);
                    if (info.actionGroups.Count == 0)
                    {
                        info.actionGroups.Add(action);
                    }
                    else if (!info.actionGroups[^1].TryMerge(action))
                    {
                        Debug.Log("Failed merge");
                        info.actionGroups.Add(action);
                    }
                    else
                    {
                        Debug.Log("Post merge: " + info.actionGroups[^1]);
                    }
                }
            }

            return shapeKeys;
        }
    }
}