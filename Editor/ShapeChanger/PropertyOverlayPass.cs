﻿#region

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
            if (context.AvatarRootObject.GetComponentInChildren<ModularAvatarShapeChanger>() != null)
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
            public ActionGroupKey(AnimationServicesContext asc, TargetProp key, GameObject controllingObject, float value)
            {
                TargetProp = key;
                InitiallyActive = controllingObject?.activeInHierarchy == true;

                var origControlling = controllingObject?.name ?? "<null>";
                while (controllingObject != null && !asc.TryGetActiveSelfProxy(controllingObject, out _))
                {
                    controllingObject = controllingObject.transform.parent?.gameObject;
                    if (controllingObject != null && RuntimeUtil.IsAvatarRoot(controllingObject.transform))
                    {
                        controllingObject = null;
                    }
                }

                var newControlling = controllingObject?.name ?? "<null>";
                Debug.Log("AGK: Controlling object " + origControlling + " => " + newControlling);

                ControllingObject = controllingObject;
                Value = value;
            }

            public TargetProp TargetProp;
            public float Value;

            public float ConditionKey;
            // When constructing the 1D blend tree to interpret the sum-of-condition-keys value, we need to ensure that
            // all valid values are solidly between two control points with the same animation clip, to avoid undesired
            // interpolation. This is done by constructing a "guard band":
            //   [ valid range ] [ guard band ] [ valid range ]
            //
            // The valid range must contain all values that could be created by valid summations. We therefore reserve
            // a "guard band" in between; by reserving the exponent below each valid stop, we can put our guard bands
            // in there.
            //  [ valid ] [ guard ] [ valid ]
            //  ^-r0      ^-g0    ^-g1
            //                      ^- r1
            // g0 = r1 / 2 = r0 * 2
            // g1 = BitDecrement(r1) (we don't actually use this currently as r0-g0 is enough)

            public float Guard => ConditionKey * 2;

            public bool ConditionKeyIsValid => float.IsFinite(ConditionKey)
                                               && float.IsFinite(Guard)
                                               && ConditionKey > 0;

            public GameObject ControllingObject;
            public bool InitiallyActive;
            public bool IsDelete;

            public override string ToString()
            {
                var obj = ControllingObject?.name ?? "<null>";

                return $"AGK: {TargetProp}={Value} " +
                       $"range={ConditionKey}/{Guard} controlling object={obj}";
            }

            public bool TryMerge(ActionGroupKey other)
            {
                if (!TargetProp.Equals(other.TargetProp)) return false;
                if (Mathf.Abs(Value - other.Value) > 0.001f) return false;
                if (ControllingObject != other.ControllingObject) return false;
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
            PreprocessShapes(shapes, out var initialStates, out var deletedShapes);
            
            ProcessInitialStates(initialStates);
            
            foreach (var groups in shapes.Values)
            {
                ProcessShapeKey(groups);
            }

            ProcessMeshDeletion(deletedShapes);
        }

        private void PreprocessShapes(Dictionary<TargetProp, PropGroup> shapes, out Dictionary<TargetProp, float> initialStates, out HashSet<TargetProp> deletedShapes)
        {
            // For each shapekey, determine 1) if we can just set an initial state and skip and 2) if we can delete the
            // corresponding mesh. If we can't, delete ops are merged into the main list of operations.
            
            initialStates = new Dictionary<TargetProp, float>();
            deletedShapes = new HashSet<TargetProp>();

            foreach (var (key, info) in shapes.ToList())
            {
                var deletions = info.actionGroups.Where(agk => agk.IsDelete).ToList();
                if (deletions.Any(d => d.ControllingObject == null))
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
                if (info.actionGroups[^1].ControllingObject == null)
                {
                    shapes.Remove(key);
                }
            }
        }

        private void ProcessInitialStates(Dictionary<TargetProp, float> initialStates)
        {
            var clips = context.Extension<AnimationServicesContext>().AnimationDatabase;
            var initialStateHolder = clips.ClipsForPath(PropertyOverlayPrePass.TAG_PATH).FirstOrDefault();
            if (initialStateHolder == null) return;

            _initialStateClip = new AnimationClip();
            _initialStateClip.name = "MA Shape Changer Defaults";
            initialStateHolder.CurrentClip = _initialStateClip;

            foreach (var (key, initialState) in initialStates)
            {
                var curve = new AnimationCurve();
                curve.AddKey(0, initialState);
                curve.AddKey(1, initialState);

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

                    if (key.PropertyName.StartsWith("blendShape."))
                    {
                        var blendShape = key.PropertyName.Substring("blendShape.".Length);
                        var index = smr.sharedMesh?.GetBlendShapeIndex(blendShape);

                        if (index != null && index >= 0) smr.SetBlendShapeWeight(index.Value, initialState);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Invalid target object: " + key.TargetObject);
                }
                
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
            if (info.actionGroups[^1].ControllingObject == null)
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

            var lastConstant = info.actionGroups.FindLastIndex(agk => agk.ControllingObject == null);
            var transitionBuffer = new List<(AnimatorState, List<AnimatorStateTransition>)>();
            var entryTransitions = new List<AnimatorTransition>();

            transitionBuffer.Add((initialState, new List<AnimatorStateTransition>()));

            foreach (var group in info.actionGroups.Skip(lastConstant))
            {
                y += yInc;

                var clip = AnimResult(group.TargetProp, group.Value);

                if (group.ControllingObject == null)
                {
                    clip.name = "Property Overlay constant " + group.Value;
                    initialState.motion = clip;
                }
                else
                {
                    clip.name = "Property Overlay controlled by " + group.ControllingObject.name + " " + group.Value;

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
                    state.name = group.ControllingObject.name;
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
                            mode = AnimatorConditionMode.Less,
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

            var controller = group.ControllingObject.transform;
            while (controller != null && !RuntimeUtil.IsAvatarRoot(controller))
            {
                if (asc.TryGetActiveSelfProxy(controller.gameObject, out var paramName))
                {
                    initialValues[paramName] = controller.gameObject.activeSelf ? 1 : 0;
                    conditions.Add(new AnimatorCondition
                    {
                        parameter = paramName,
                        mode = AnimatorConditionMode.Greater,
                        threshold = 0.5f
                    });
                }

                controller = controller.parent;
            }

            if (conditions.Count == 0) throw new InvalidOperationException("No controlling object found for " + group);

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
                        var agk = new ActionGroupKey(asc, key, null, value);
                        agk.InitiallyActive = true;
                        agk.Value = renderer.GetBlendShapeWeight(shapeId);
                        info.actionGroups.Add(agk);
                    }

                    var action = new ActionGroupKey(asc, key, changer.gameObject, value);
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
                    if (action.ControllingObject == null)
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