#nullable enable
#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal sealed class VRChatBlendTreeBackend : IReactionBackend
    {
        internal const string BaseLayerName = "MA/RC Base";
        internal const string ApplyLayerName = "MA/RC Apply";

        private readonly VirtualAnimatorController _fxController;
        private readonly UnityBlendTreeBackend _inner;
        public VRChatBlendTreeBackend(
            VirtualAnimatorController fxController,
            UnityBlendTreeBackend inner)
        {
            _fxController = fxController ?? throw new ArgumentNullException(nameof(fxController));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
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

        public string AddParameter(string prefix, float initialValue)
        {
            return _inner.AddParameter(prefix, initialValue);
        }

        public float GetParameterInitialValue(string name)
        {
            return _inner.GetParameterInitialValue(name);
        }

        public void SetParameterInitialValue(string name, float value)
        {
            _inner.SetParameterInitialValue(name, value);
        }

        public void Build(ReactionGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            PreprocessMeshSections(graph);
            _inner.Build(graph);
            if (!_inner.HasGeneratedOutput) return;

            InstallLayer(BaseLayerName, int.MinValue, "Base", _inner.BaseLayerTree);
            InstallLayer(ApplyLayerName, 1, "Apply", _inner.RootTree);
        }

        private void InstallLayer(string layerName, int priority, string stateName, VirtualMotion motion)
        {
            var layer = _fxController.AddLayer(new LayerPriority(priority), layerName);
            layer.BlendingMode = AnimatorLayerBlendingMode.Override;
            layer.DefaultWeight = 1;

            var stateMachine = layer.StateMachine ??
                               throw new InvalidOperationException("Animator layer was created without a state machine");
            var state = stateMachine.AddState(stateName);
            stateMachine.DefaultState = state;
            state.Motion = motion;
        }
    }
}
#endif
