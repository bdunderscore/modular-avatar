#nullable enable
#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDKBase;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal sealed class VRChatBlendTreeBackend : IReactionBackend
    {
        internal const string BaseLayerName = "MA/RC Base";
        internal const string ApplyLayerName = "MA/RC Apply";

        private readonly UnityBlendTreeBackend _inner;

        internal VirtualAnimatorController FX { get; }

        public VRCExpressionParameters.Parameter[] ExtraSyncParams
        {
            get
            {
                if (_syncInitParam == null) return Array.Empty<VRCExpressionParameters.Parameter>();

                return new[]
                {
                    new VRCExpressionParameters.Parameter
                    {
                        name = _syncInitParam,
                        valueType = VRCExpressionParameters.ValueType.Bool,
                        defaultValue = 0,
                        networkSynced = true,
                        saved = false
                    }
                };
            }
        }
        
        public VRChatBlendTreeBackend(
            VirtualAnimatorController fxController,
            UnityBlendTreeBackend inner
        )
        {
            FX = fxController ?? throw new ArgumentNullException(nameof(fxController));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _inner._effectProcessors.Add(new DeltaTimeProcessor(this));
        }

        public void PreprocessGraph(ReactionGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            // Audio handling requires the original semantic active-state actions. Do this before
            // the Unity backend imports externally animated object state.
            PreprocessControlledAudioSources(graph);
            _inner.PreprocessGraph(graph);
        }

        private void PreprocessMeshSections(ReactionGraph graph)
        {
            var initiallyActiveEffects = GetInitiallyActiveMeshEffects(graph);
            var targetsByRenderer = graph.Nodes
                .SelectMany(node => node.Effects.OfType<HideMeshSection>())
                .Where(effect => effect.ShouldHide)
                .GroupBy(effect => effect.Target.Renderer)
                .ToList();

            foreach (var rendererGroup in targetsByRenderer)
            {
                var renderer = rendererGroup.Key;
                var targetSelectors = rendererGroup
                    .GroupBy(effect => effect.Target)
                    .ToDictionary(
                        group => group.Key,
                        group => MeshFilterUtilities.AggregateVertexFilters(
                            group.Select(effect => effect.Selector!))
                    );

                if (renderer == null || renderer.sharedMesh == null)
                {
                    RemoveMeshSectionEffects(graph, targetSelectors.Keys, new HashSet<MeshSectionTarget>());
                    continue;
                }

                var mesh = renderer.sharedMesh;
                var targets = targetSelectors.Select(pair => (pair.Key, pair.Value)).ToList();
                var plan = NaNimationFilter.ComputeNaNPlan(renderer, ref mesh, targets);
                renderer.sharedMesh = mesh;

                var plannedTargets = plan.Keys.Select(key => key.Item1).ToHashSet();
                var generatedBones = plan.Count == 0
                    ? new Dictionary<(MeshSectionTarget, IMeshSelector), List<GameObject>>()
                    : NaNimationFilter.GenerateNaNimatedBones(renderer, plan);
                var generatedBonesByTarget = generatedBones
                    .GroupBy(pair => pair.Key.Item1)
                    .ToDictionary(
                        group => group.Key,
                        group => group.SelectMany(pair => pair.Value).ToList());

                AddInitialConstraints(generatedBonesByTarget, initiallyActiveEffects);
                RemoveMeshSectionEffects(graph, targetSelectors.Keys, plannedTargets, generatedBonesByTarget);
            }

            foreach (var node in graph.Nodes)
                node.Effects.RemoveAll(effect => effect is HideMeshSection { ShouldHide: false });

            graph.Nodes.RemoveAll(node => node.Effects.Count == 0);
        }

        private static Dictionary<MeshSectionTarget, IAction> GetInitiallyActiveMeshEffects(ReactionGraph graph)
        {
            var activeEffects = new Dictionary<MeshSectionTarget, IAction>();
            var objectStates = GetInitiallyActiveObjectStates(graph);
            var context = new ExpressionEvaluationContext(
                graph.Parameters.GetParameterInitialValue,
                target => objectStates.TryGetValue(target, out var active) ? active : target.activeSelf);

            foreach (var node in graph.Nodes)
            {
                if (!node.Expression.Evaluate(context))
                    continue;

                foreach (var effect in node.Effects)
                {
                    if (effect is HideMeshSection hide)
                        activeEffects[hide.Target] = hide;
                }
            }

            return activeEffects;
        }

        private static Dictionary<GameObject, bool> GetInitiallyActiveObjectStates(ReactionGraph graph)
        {
            var objectStates = graph.Nodes
                .SelectMany(node => node.Effects.Select(UnwrapAlreadyApplied).OfType<DriveActiveState>())
                .Select(action => action.Target)
                .Distinct()
                .ToDictionary(target => target, target => target.activeSelf);
            var context = new ExpressionEvaluationContext(
                graph.Parameters.GetParameterInitialValue,
                target => objectStates.TryGetValue(target, out var active) ? active : target.activeSelf);

            // A driven object can gate another driver, so resolve the initial states in
            // simultaneous rounds. An acyclic chain converges in at most one round per
            // driven object; the extra round detects convergence.
            for (var iteration = 0; iteration <= objectStates.Count; iteration++)
            {
                var nextStates = new Dictionary<GameObject, bool>(objectStates);
                foreach (var node in graph.Nodes)
                {
                    if (!node.Expression.Evaluate(context)) continue;
                    foreach (var effect in node.Effects.Select(UnwrapAlreadyApplied).OfType<DriveActiveState>())
                    {
                        nextStates[effect.Target] = effect.Active;
                    }
                }

                if (objectStates.All(state => nextStates[state.Key] == state.Value))
                    return nextStates;

                objectStates = nextStates;
            }

            return objectStates;
        }

        private static IAction UnwrapAlreadyApplied(IAction action)
        {
            return action is AlreadyApplied alreadyApplied ? alreadyApplied.Inner : action;
        }


        private void RemoveMeshSectionEffects(
            ReactionGraph graph,
            IEnumerable<MeshSectionTarget> allTargets,
            HashSet<MeshSectionTarget> plannedTargets,
            Dictionary<MeshSectionTarget, List<GameObject>>? generatedBones = null)
        {
            var targets = allTargets.ToHashSet();
            var noOpShapeTargets = targets
                .Where(target => !plannedTargets.Contains(target) && target.IsShape)
                .ToHashSet();

            foreach (var node in graph.Nodes)
            {
                var replacementEffects = new List<IAction>();
                foreach (var effect in node.Effects)
                {
                    if (effect is HideMeshSection hide && targets.Contains(hide.Target))
                    {
                        if (plannedTargets.Contains(hide.Target))
                            replacementEffects.AddRange(CreateNaNimationFloatActions(
                                hide.Target.Renderer,
                                generatedBones![hide.Target],
                                hide.ShouldHide));
                        continue;
                    }

                    if (effect is SetShapeKey shape &&
                        noOpShapeTargets.Any(target => Equals(target.Renderer, shape.Renderer) &&
                                                       target.ShapeName == shape.ShapeName))
                        continue;

                    replacementEffects.Add(effect);
                }

                node.Effects = replacementEffects;
            }
        }

        private static IEnumerable<IAction> CreateNaNimationFloatActions(
            SkinnedMeshRenderer renderer,
            IEnumerable<GameObject> bones,
            bool shouldHide)
        {
            foreach (var bone in bones)
            {
                foreach (var dimension in new[] { "x", "y", "z" })
                {
                    yield return new FloatPropAction(
                        new PropertyTarget(bone.transform, $"m_LocalScale.{dimension}"),
                        shouldHide ? float.NaN : 1f);
                }
            }

            yield return new FloatPropAction(
                new PropertyTarget(renderer, "m_UpdateWhenOffscreen"),
                0f);
        }

        private void AddInitialConstraints(
            Dictionary<MeshSectionTarget, List<GameObject>> generatedBones,
            Dictionary<MeshSectionTarget, IAction> initiallyActiveEffects)
        {
            var constrainedBones = new HashSet<GameObject>();
            foreach (var (target, bones) in generatedBones)
            {
                if (!initiallyActiveEffects.TryGetValue(target, out var effect) ||
                    effect is not HideMeshSection { ShouldHide: true })
                    continue;

                foreach (var bone in bones)
                {
                    if (!constrainedBones.Add(bone)) continue;

                    var constraint = bone.AddComponent<VRCScaleConstraint>();
                    constraint.Sources.Add(new VRCConstraintSource
                    {
                        SourceTransform = constraint.transform,
                        Weight = float.NaN
                    });
                    constraint.GlobalWeight = float.NaN;
                    constraint.Locked = true;
                    constraint.IsActive = true;

                    var path = _inner.ObjectPathRemapper.GetVirtualPathForObject(bone);
                    _inner.BaseLayerClip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(path, typeof(VRCScaleConstraint), "IsActive"),
                        AnimationCurve.Constant(0, 1, 0));
                    _inner.BaseLayerClip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(path, typeof(VRCScaleConstraint), "GlobalWeight"),
                        AnimationCurve.Constant(0, 1, 0));
                }
            }
        }

        private void PreprocessControlledAudioSources(ReactionGraph graph)
        {
            var controlledTargets = graph.Nodes
                .SelectMany(node => node.Effects.OfType<DriveActiveState>())
                .Select(action => action.Target)
                .Where(target => target != null)
                .Distinct();
            var processedSources = new HashSet<AudioSource>();

            foreach (var target in controlledTargets)
            {
                foreach (var source in target.GetComponentsInChildren<AudioSource>(true))
                {
                    if (!processedSources.Add(source)) continue;

                    var binding = EditorCurveBinding.FloatCurve(
                        _inner.ObjectPathRemapper.GetVirtualPathForObject(source.gameObject),
                        typeof(AudioSource),
                        "m_Enabled");
                    _inner.BaseLayerClip.SetFloatCurve(binding,
                        AnimationCurve.Constant(0, 1, source.enabled ? 1 : 0));
                    source.enabled = false;
                }
            }
        }

        public string AddUniqueParameter(string prefix, float initialValue)
        {
            return _inner.AddUniqueParameter(prefix, initialValue);
        }

        public float GetParameterInitialValue(string name)
        {
            return _inner.GetParameterInitialValue(name);
        }

        public void SetParameterInitialValue(string name, float value)
        {
            _inner.SetParameterInitialValue(name, value);
        }


        private readonly struct ReactiveMoveEntry
        {
            internal ReactionNode Node { get; }
            internal ReactiveMoveAction Action { get; }

            internal ReactiveMoveEntry(ReactionNode node, ReactiveMoveAction action)
            {
                Node = node;
                Action = action;
            }
        }

        private sealed class ReactiveMoveConstraint
        {
            internal VRCConstraintBase Constraint { get; }
            internal IReadOnlyList<ReactiveMoveEntry> Sources { get; }

            internal ReactiveMoveConstraint(
                VRCConstraintBase constraint,
                IReadOnlyList<ReactiveMoveEntry> sources
            )
            {
                Constraint = constraint;
                Sources = sources;
            }
        }

        private static void LowerReactiveMoves(ReactionGraph graph)
        {
            var entries = new List<ReactiveMoveEntry>();

            foreach (var node in graph.Nodes)
            {
                foreach (var effect in node.Effects)
                {
                    if (effect is ReactiveMoveAction action)
                        entries.Add(new ReactiveMoveEntry(node, action));
                }
            }

            var prependActions = new List<ReactionNode>();

            foreach (var targetGroup in entries.GroupBy(entry => entry.Action.ToMove))
            {
                var constraints = CreateReactiveMoveConstraints(
                    targetGroup.Key,
                    targetGroup.ToList()
                );

                foreach (var constraint in constraints)
                {
                    var sourceToIndex = constraint.Constraint.Sources
                        .Select((source, index) => (source.SourceTransform, Index: index))
                        .ToDictionary(pair => pair.SourceTransform, pair => pair.Index);
                    var baselineProps = ConstraintProperties(constraint.Constraint, 0f, 0f);
                    var anyHasTransitionTime = constraint.Sources.Any(source =>
                        !source.Action.FixToWorld && source.Action.TransitionTime > 0);

                    if (anyHasTransitionTime)
                    {
                        var baseline = new ReactionNode(
                            new Constant(true),
                            new DeltaTimeControlAction(
                                constraint.Constraint,
                                0,
                                null,
                                SourceWeightProperty(0)
                            )
                            {
                                ConstProps = baselineProps
                            }
                        )
                        {
                            Priority = int.MinValue
                        };
                        prependActions.Add(baseline);

                        foreach (var trigger in constraint.Sources)
                        {
                            var action = trigger.Action;
                            if (action.FixToWorld)
                            {
                                trigger.Node.Effects.Add(new DeltaTimeControlAction(
                                    constraint.Constraint,
                                    0,
                                    null,
                                    SourceWeightProperty(0)
                                )
                                {
                                    ConstProps = ConstraintProperties(constraint.Constraint, 1f, 1f)
                                });
                                continue;
                            }

                            if (action.WhereTo == null)
                            {
                                trigger.Node.Effects.Add(new DeltaTimeControlAction(
                                    constraint.Constraint,
                                    0,
                                    null,
                                    SourceWeightProperty(0)
                                )
                                {
                                    ConstProps = baselineProps
                                });
                                continue;
                            }

                            var sourceIndex = sourceToIndex[action.WhereTo];
                            trigger.Node.Effects.Add(new DeltaTimeControlAction(
                                constraint.Constraint,
                                action.TransitionTime,
                                SourceWeightProperty(sourceIndex),
                                SourceWeightProperty(0)
                            )
                            {
                                ConstProps = ConstraintProperties(constraint.Constraint, 1f, 0f)
                                    .Remove(SourceWeightProperty(sourceIndex))
                            });
                        }
                    }
                    else
                    {
                        var baseline = new ReactionNode(new Constant(true))
                        {
                            Priority = int.MinValue
                        };
                        foreach (var (property, value) in baselineProps)
                        {
                            baseline.Effects.Add(new FloatPropAction(
                                new PropertyTarget(constraint.Constraint, property), value));
                        }

                        prependActions.Add(baseline);

                        foreach (var trigger in constraint.Sources)
                        {
                            var action = trigger.Action;
                            if (action.WhereTo == null && !action.FixToWorld)
                            {
                                foreach (var (property, value) in baselineProps)
                                {
                                    trigger.Node.Effects.Add(new FloatPropAction(
                                        new PropertyTarget(constraint.Constraint, property), value));
                                }
                                continue;
                            }

                            var values = ConstraintProperties(
                                constraint.Constraint,
                                1f,
                                action.FixToWorld ? 1f : 0f
                            );
                            if (!action.FixToWorld)
                            {
                                values = values.SetItem(
                                    SourceWeightProperty(sourceToIndex[action.WhereTo!]),
                                    1f
                                );
                            }

                            foreach (var (property, value) in values)
                            {
                                trigger.Node.Effects.Add(new FloatPropAction(
                                    new PropertyTarget(constraint.Constraint, property), value));
                            }
                        }
                    }
                }
            }

            graph.Nodes.InsertRange(0, prependActions);
        }

        private static ImmutableDictionary<string, float> ConstraintProperties(
            VRCConstraintBase constraint,
            float globalWeight,
            float freezeToWorld
        )
        {
            var properties = ImmutableDictionary<string, float>.Empty
                .Add("GlobalWeight", globalWeight)
                .Add("FreezeToWorld", freezeToWorld);
            for (var sourceIndex = 0; sourceIndex < constraint.Sources.Count; sourceIndex++)
                properties = properties.Add(SourceWeightProperty(sourceIndex), 0f);
            return properties;
        }

        private static IReadOnlyList<ReactiveMoveConstraint> CreateReactiveMoveConstraints(
            Transform toMove,
            IReadOnlyList<ReactiveMoveEntry> moves
        )
        {
            var constraints = new List<ReactiveMoveConstraint>();

            if (moves.Any(move => move.Action.SetsPosition))
            {
                var positionMoves = moves.Where(move => move.Action.SetsPosition).ToList();
                var positionConstraint = toMove.gameObject.AddComponent<VRCPositionConstraint>();
                positionConstraint.PositionAtRest = toMove.localPosition;
                positionConstraint.PositionOffset = Vector3.zero;
                positionConstraint.AffectsPositionX = true;
                positionConstraint.AffectsPositionY = true;
                positionConstraint.AffectsPositionZ = true;
                var positionReactiveConstraint = AddSources(
                    positionConstraint,
                    positionMoves,
                    positionMoves.Any(move => !move.Action.FixToWorld && move.Action.TransitionTime > 0)
                );
                ConfigureConstraint(positionConstraint);
                constraints.Add(positionReactiveConstraint);
            }

            if (moves.Any(move => move.Action.SetsRotation))
            {
                var rotationMoves = moves.Where(move => move.Action.SetsRotation).ToList();
                var rotationConstraint = toMove.gameObject.AddComponent<VRCRotationConstraint>();
                rotationConstraint.RotationAtRest = toMove.localEulerAngles;
                rotationConstraint.RotationOffset = Vector3.zero;
                rotationConstraint.AffectsRotationX = true;
                rotationConstraint.AffectsRotationY = true;
                rotationConstraint.AffectsRotationZ = true;
                var rotationReactiveConstraint = AddSources(
                    rotationConstraint,
                    rotationMoves,
                    rotationMoves.Any(move => !move.Action.FixToWorld && move.Action.TransitionTime > 0)
                );
                ConfigureConstraint(rotationConstraint);
                constraints.Add(rotationReactiveConstraint);
            }

            if (moves.Any(move => move.Action.SetsScale))
            {
                var scaleMoves = moves.Where(move => move.Action.SetsScale).ToList();
                var scaleConstraint = toMove.gameObject.AddComponent<VRCScaleConstraint>();
                scaleConstraint.ScaleAtRest = toMove.localScale;
                scaleConstraint.ScaleOffset = Vector3.one;
                scaleConstraint.AffectsScaleX = true;
                scaleConstraint.AffectsScaleY = true;
                scaleConstraint.AffectsScaleZ = true;
                var scaleReactiveConstraint = AddSources(
                    scaleConstraint,
                    scaleMoves,
                    scaleMoves.Any(move => !move.Action.FixToWorld && move.Action.TransitionTime > 0)
                );
                ConfigureConstraint(scaleConstraint);
                constraints.Add(scaleReactiveConstraint);
            }

            return constraints;
        }


        private static void ConfigureConstraint(VRCConstraintBase constraint)
        {
            constraint.IsActive = true;
            constraint.Locked = true;
            constraint.GlobalWeight = 0;
            constraint.SolveInLocalSpace = false;
        }

        private static ReactiveMoveConstraint AddSources(
            VRCConstraintBase constraint,
            IEnumerable<ReactiveMoveEntry> moves,
            bool createSelfReference
        )
        {
            var sources = moves.ToList();

            if (createSelfReference)
            {
                constraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = constraint.transform,
                    Weight = 0,
                    ParentPositionOffset = Vector3.zero,
                    ParentRotationOffset = Vector3.zero
                });
            }

            foreach (var target in sources.Select(source => source.Action.WhereTo).Where(target => target != null)
                         .Distinct())
            {
                constraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = target,
                    Weight = 0,
                    ParentPositionOffset = Vector3.zero,
                    ParentRotationOffset = Vector3.zero
                });
            }

            return new ReactiveMoveConstraint(constraint, sources);
        }

        private static string SourceWeightProperty(int sourceIndex)
        {
            return sourceIndex < 16
                ? $"Sources.source{sourceIndex}.Weight"
                : $"Sources.overflowList.Array.data[{sourceIndex - 16}].Weight";
        }

        public void Build(ReactionGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            PreprocessMeshSections(graph);
            LowerReactiveMoves(graph);
            _inner.Build(graph);
            if (!_inner.HasGeneratedOutput) return;

            InstallLayer(BaseLayerName, int.MinValue, "Base", _inner.BaseLayerTree);
            var applyState = InstallLayer(ApplyLayerName, 1, "Apply", _inner.RootTree);

            if (_syncInitParam != null)
            {
                var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
                {
                    new()
                    {
                        name = _syncInitParam,
                        value = 1,
                        type = VRC_AvatarParameterDriver.ChangeType.Set
                    }
                };

                applyState.Behaviours = ImmutableList<StateMachineBehaviour>.Empty.Add(driver);
            }
        }

        private VirtualState InstallLayer(string layerName, int priority, string stateName, VirtualMotion motion)
        {
            var layer = FX.AddLayer(new LayerPriority(priority), layerName);
            layer.BlendingMode = AnimatorLayerBlendingMode.Override;
            layer.DefaultWeight = 1;

            var stateMachine = layer.StateMachine ??
                               throw new InvalidOperationException("Animator layer was created without a state machine");
            var state = stateMachine.AddState(stateName);
            stateMachine.DefaultState = state;
            state.Motion = motion;

            return state;
        }

        private string? _syncInitParam, _syncInitCounter;

        public string SyncInitParam
        {
            get
            {
                if (_syncInitParam == null)
                {
                    _syncInitParam = "__MA/SyncInit";
                    _inner.EnsureParameterPresent(_syncInitParam);
                    _syncInitCounter = "__MA/SyncInit/Counter";
                    _inner.EnsureParameterPresent(_syncInitCounter);
                    CreateSyncInitTree();
                }

                return _syncInitCounter!;
            }
        }

        private void CreateSyncInitTree()
        {
            var addToSyncCounter = VirtualClip.Create("AddToSyncCounter");
            addToSyncCounter.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), _syncInitCounter),
                AnimationCurve.Constant(0, 1, 1)
            );
            
            _inner.BaseLayerTree.Children = _inner.BaseLayerTree.Children
                .Add(new()
                {
                    Motion = addToSyncCounter,
                    DirectBlendParameter = _syncInitParam!
                })
                .Add(new()
                {
                    Motion = addToSyncCounter,
                    DirectBlendParameter = _syncInitCounter!
                });
        }

        private string? _deltaTimeParam, _elapsedTimeParam;

        internal string DeltaTimeParam
        {
            get
            {
                if (_deltaTimeParam == null)
                {
                    BuildDeltaTimeLayers(out _elapsedTimeParam, out _deltaTimeParam);
                }

                return _deltaTimeParam;
            }
        }

        internal string ElapsedTimeParam
        {
            get
            {
                if (_elapsedTimeParam == null)
                {
                    BuildDeltaTimeLayers(out _elapsedTimeParam, out _deltaTimeParam);
                }

                return _elapsedTimeParam;
            }
        }

        public ObjectPathRemapper ObjectPathRemapper => _inner.ObjectPathRemapper;

        private void BuildDeltaTimeLayers(out string elapsedTimeParam, out string deltaTimeParam)
        {
            elapsedTimeParam = _inner.AddUniqueParameter("MA/RC/ElapsedTime", 0);
            var delayElapsedTime = _inner.AddUniqueParameter("MA/RC/ElapsedTime/Delay", 0);
            deltaTimeParam = _inner.AddUniqueParameter("MA/RC/DeltaTime", 0);

            var elapsedTimeClip = VirtualClip.Create("Elapsed time");
            elapsedTimeClip.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), elapsedTimeParam),
                AnimationCurve.Linear(0, 0, 1_000_000, 1_000_000)
            );
            var settings = elapsedTimeClip.Settings;
            settings.loopTime = true;
            elapsedTimeClip.Settings = settings;

            var deltaPositive = VirtualClip.Create("DeltaPos");
            deltaPositive.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), deltaTimeParam),
                AnimationCurve.Constant(0, 1, 1)
            );
            var deltaNegative = VirtualClip.Create("DeltaNeg");
            deltaNegative.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), deltaTimeParam),
                AnimationCurve.Constant(0, 1, -1)
            );
            var delay = VirtualClip.Create("Delay");
            delay.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), delayElapsedTime),
                AnimationCurve.Constant(0, 1, 1)
            );

            var bt = VirtualBlendTree.Create("RC DeltaTime");
            bt.BlendType = BlendTreeType.Direct;
            bt.Children = ImmutableList<VirtualBlendTree.VirtualChildMotion>.Empty
                .Add(new VirtualBlendTree.VirtualChildMotion
                {
                    DirectBlendParameter = UnityBlendTreeBackend.ALWAYS_ONE,
                    Motion = elapsedTimeClip
                })
                .Add(new VirtualBlendTree.VirtualChildMotion
                {
                    DirectBlendParameter = elapsedTimeParam,
                    Motion = delay
                })
                .Add(new VirtualBlendTree.VirtualChildMotion
                {
                    DirectBlendParameter = elapsedTimeParam,
                    Motion = deltaPositive
                })
                .Add(new VirtualBlendTree.VirtualChildMotion
                {
                    DirectBlendParameter = delayElapsedTime,
                    Motion = deltaNegative
                });

            _inner.RootTree.Children = _inner.RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                DirectBlendParameter = UnityBlendTreeBackend.ALWAYS_ONE,
                Motion = bt
            });
        }
    }
}
#endif
