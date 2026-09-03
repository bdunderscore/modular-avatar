#nullable enable


using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using UnityEditor;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactiveObjectPassV2
    {
        private readonly ndmf.BuildContext context;
        private readonly AnimatorServicesContext asc;
        private UnityBlendTreeBackend? _blendTreeBackend;
        private readonly Dictionary<TargetProp, List<GameObject>> _nanBonesForProp = new();

        public ReactiveObjectPassV2(ndmf.BuildContext context)
        {
            this.context = context;
            asc = context.Extension<AnimatorServicesContext>();
        }

        internal void Execute()
        {
            var analysis = new ReactiveObjectAnalyzer(context).Analyze(context.AvatarRootObject);

            var shapes = analysis.Shapes;
            var initialStates = analysis.InitialStates;

            PreProcessMeshDeletion(shapes, initialStates);

            if (context.PlatformProvider.QualifiedName == WellKnownPlatforms.VRChatAvatar30)
            {
                // Drop constant shapes that have no preexisting foreign animations (apply their value
                // directly to the scene object). Shapes that DO have foreign animations must be kept so
                // the apply layer can override them.
                RemoveRedundantConstantShapes(shapes, initialStates);

                var requiresAnimation = shapes.Values.Any(p => p.actionGroups.Count > 0 || p.overrideStaticState != null);
                if (requiresAnimation)
                {
#if MA_VRCSDK3_AVATARS
                    var controller = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
                    var innerBackend = new UnityBlendTreeBackend(context, controller);
                    _blendTreeBackend = innerBackend;
                    IReactionBackend backend = new VRChatBlendTreeBackend(controller, innerBackend);
                    var graph = ShapeToGraph(shapes);
                    backend.PreprocessGraph(graph);
                    backend.Build(ILBuild.Optimize(backend, graph));
#endif
                }
            }

            // Apply the initially-active state to scene objects for all remaining (non-constant)
            // props. This must run after backend generation so base-state capture has already read the original
            // scene values into BaseLayerClip before we overwrite them here.
            ApplyInitialSceneStates(shapes, initialStates);

            ApplyStaticStateOverrides(shapes);
        }

        private void RemoveRedundantConstantShapes(
            Dictionary<TargetProp, AnimatedProperty> shapes,
            Dictionary<TargetProp, object?> initialStates)
        {
            var constantShapes = shapes
                .Where(kv => kv.Value.actionGroups.LastOrDefault()?.IsConstant is true)
                .Where(kv => kv.Value.actionGroups.All(x => x.Value is not IMeshSelector))
                .Where(kv => kv.Value.overrideStaticState == null)
                .ToList();

            foreach (var (key, _) in constantShapes)
            {
                GameObject gameObject;
                switch (key.TargetObject)
                {
                    case GameObject go: gameObject = go; break;
                    case Component c: gameObject = c.gameObject; break;
                    default: continue;
                }

                var path = asc.ObjectPathRemapper.GetVirtualPathForObject(gameObject);
                var property = new SerializedObject(key.TargetObject).FindProperty(key.PropertyName);
                var ecb = property?.propertyType == SerializedPropertyType.ObjectReference
                    ? EditorCurveBinding.PPtrCurve(path, key.TargetObject.GetType(), key.PropertyName)
                    : EditorCurveBinding.FloatCurve(path, key.TargetObject.GetType(), key.PropertyName);

                // If any preexisting clip already animates this binding, keep the shape so the
                // apply layer can override it.
                if (asc.AnimationIndex.GetClipsForBinding(ecb).Any()) continue;

                shapes.Remove(key);

                // Apply the constant value directly to the scene object since no animation will.
                if (!initialStates.TryGetValue(key, out var constantValue))
                    continue;

                ApplyValueToSceneObject(key, constantValue);
            }
        }

        private static void ApplyInitialSceneStates(
            Dictionary<TargetProp, AnimatedProperty> shapes,
            Dictionary<TargetProp, object?> initialStates)
        {
            foreach (var (key, prop) in shapes)
            {
                if (!prop.actionGroups.Any(ag => ag.InitiallyActive)) continue;
                if (!initialStates.TryGetValue(key, out var value)) continue;
                ApplyValueToSceneObject(key, value);
            }
        }

        private static void ApplyValueToSceneObject(TargetProp key, object? value)
        {
            if (key.TargetObject is SkinnedMeshRenderer smr &&
                key.PropertyName.StartsWith(ReactiveObjectAnalyzer.BlendshapePrefix))
            {
                var shapeName = key.PropertyName[ReactiveObjectAnalyzer.BlendshapePrefix.Length..];
                var index = smr.sharedMesh?.GetBlendShapeIndex(shapeName) ?? -1;
                if (index >= 0)
                    smr.SetBlendShapeWeight(index, (float)value);
                return;
            }

            var so = new SerializedObject(key.TargetObject);
            var sprop = so.FindProperty(key.PropertyName);
            if (sprop == null) return;

            switch (sprop.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    sprop.boolValue = (float)value > 0.5f;
                    break;
                case SerializedPropertyType.Float:
                    sprop.floatValue = (float)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    sprop.objectReferenceValue = value as Object;
                    break;
                default:
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ApplyStaticStateOverrides(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            // TODO - this function as a whole is a unity/vrchat-specific concern. However, the current
            // hack of using overrideStaticState disappears in ReactionGraph, so we need to handle it here.
            // In the future, we'll use higher level IActions in an initial portable graph representation,
            // and derive static state overrides inside the VRChat backend from those high level actions.
            foreach (var (key, prop) in shapes)
            {
                if (prop.overrideStaticState == null) continue;

                var so = new SerializedObject(key.TargetObject);
                var sprop = so.FindProperty(key.PropertyName);
                if (sprop == null) continue;

                float originalValue;
                switch (sprop.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        originalValue = sprop.boolValue ? 1f : 0f;
                        sprop.boolValue = (float)prop.overrideStaticState > 0.5f;
                        break;
                    case SerializedPropertyType.Float:
                        originalValue = sprop.floatValue;
                        sprop.floatValue = (float)prop.overrideStaticState;
                        break;
                    default:
                        continue;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                if (prop.actionGroups.Count == 0 && _blendTreeBackend != null)
                {
                    _blendTreeBackend.ApplyBaseState(key, originalValue);
                }
            }
        }

        private void PreProcessMeshDeletion(
            Dictionary<TargetProp, AnimatedProperty> shapes,
            Dictionary<TargetProp, object?> initialStates)
        {
            var rendererGroups = shapes.Values
                .Where(prop => prop.actionGroups.Any(x => x.Value is IMeshSelector))
                .GroupBy(prop => prop.TargetProp.TargetObject as SkinnedMeshRenderer)
                .ToList();

            foreach (var grouping in rendererGroups)
            {
                var renderer = grouping.Key;
                if (renderer == null) continue;

                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var toDelete = grouping
                    .Where(prop =>
                    {
                        var activeGroup = prop.actionGroups.LastOrDefault();
                        return (activeGroup?.IsConstantActive is true && activeGroup.Value is IMeshSelector) ||
                               (context.PlatformProvider.QualifiedName != WellKnownPlatforms.VRChatAvatar30 &&
                                initialStates.TryGetValue(prop.TargetProp, out var initialState) &&
                                initialState is IMeshSelector);
                    })
                    .Select(prop =>
                    {
                        if (context.PlatformProvider.QualifiedName != WellKnownPlatforms.VRChatAvatar30 &&
                            initialStates.TryGetValue(prop.TargetProp, out var initialState) &&
                            initialState is IMeshSelector selector)
                        {
                            return (prop.TargetProp, VertexFilter: selector);
                        }

                        return (prop.TargetProp,
                            VertexFilter: AggregateVertexFilters(prop.actionGroups.Select(x => x.Value as IMeshSelector)));
                    })
                    .ToList();

                var toNaNimate = grouping
                    .Where(prop => prop.actionGroups.LastOrDefault()?.IsConstantActive is false)
                    .Select(prop => (
                        prop.TargetProp,
                        VertexFilter: AggregateVertexFilters(prop.actionGroups.Select(x => x.Value as IMeshSelector))
                    ))
                    .ToList();

                renderer.sharedMesh = mesh = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, toDelete);

                foreach (var (prop, _) in toDelete)
                {
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

                if (context.PlatformProvider.QualifiedName != WellKnownPlatforms.VRChatAvatar30) continue;

                if (toNaNimate.Count == 0) continue;

                var nanPlan = NaNimationFilter.ComputeNaNPlan(renderer, ref mesh, toNaNimate);
                renderer.sharedMesh = mesh;

                if (nanPlan.Count > 0)
                {
                    var nanBones = NaNimationFilter.GenerateNaNimatedBones(renderer, nanPlan);
                    foreach (var kv in nanBones)
                    {
                        _nanBonesForProp[kv.Key.Item1] = kv.Value;
                    }
                }

                // Props for which ComputeNaNPlan generated no bones (empty/no-op filter) should
                // be removed so we don't emit a pointless animator layer.
                var nanimatedProps = nanPlan.Select(x => x.Key.Item1).ToHashSet();
                foreach (var (prop, _) in toNaNimate.Where(x => !nanimatedProps.Contains(x.TargetProp)))
                {
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

        private static IMeshSelector AggregateVertexFilters(IEnumerable<IMeshSelector?> filters)
        {
            var list = filters.ToList();
            var filter = list.LastOrDefault(f => f != null);
            if (filter is VertexFilterByShape filterByShape)
            {
                return new VertexFilterByShape(filterByShape.Shapes, list
                    .OfType<VertexFilterByShape>()
                    .Min(x => x.Threshold));
            }

            return filter ?? throw new InvalidOperationException("Expected at least one vertex filter to aggregate");
        }

        private ReactionGraph ShapeToGraph(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var graph = new ReactionGraph();

            foreach (var prop in shapes.Values)
            {
                foreach (var rule in prop.actionGroups)
                {
                    IAction action;
                    if (rule.TargetProp.TargetObject is GameObject go && rule.TargetProp.PropertyName == "m_IsActive")
                    {
                        if (rule.Value is not float activeValue)
                            throw new InvalidOperationException(
                                $"Object active state reaction for {rule.TargetProp} did not contain a float value; got {DescribeValue(rule.Value)}");
                        action = new DriveActiveState(go, activeValue > 0.5f);
                    }
                    else if (_nanBonesForProp.TryGetValue(rule.TargetProp, out var bones))
                    {
                        if (rule.Value is IMeshSelector)
                        {
#if MA_VRCSDK3_AVATARS
                            action = new NaNimationAction(rule.TargetProp, bones, true);
#else
                            continue;
#endif
                        }
                        else
                        {
                            // Non-filter rule for a NaNimated prop: the base clip handles the retain
                            // state, so no explicit action is needed here.
                            continue;
                        }
                    }
                    else if (rule.Value is IMeshSelector)
                    {
                        // No bones were generated (no-op filter or unconditional deletion already
                        // handled in pre-processing); skip this rule.
                        continue;
                    }
                    else
                    {
                        action = new PropAction(rule.TargetProp, rule.Value);
                    }

                    var conditions = rule.ControllingConditions
                        .Select(condition => ConvertCondition(graph, condition))
                        .ToArray();
                    IExpression expr = new AndNode(conditions);
                    if (rule.Inverted)
                    {
                        expr = new NotNode(expr);
                    }

                    graph.AddNode(new ReactionNode(expr, action));
                }
            }

            return graph;
        }

        private IExpression ConvertCondition(ReactionGraph graph, ControlCondition arg)
        {
            if (arg.ReferenceObject != null)
            {
                return new ObjectActiveState(arg.ReferenceObject, ObjectActiveState.State.Active);
            }

            graph.Parameters.EnsureParameter(arg.Parameter, arg.InitialValue);

            if (!float.IsFinite(arg.ParameterValueHi))
            {
                return new ParameterExpression(arg.Parameter, arg.ParameterValueLo);
            }

            if (!float.IsFinite(arg.ParameterValueLo))
            {
                return new ParameterExpression(arg.Parameter, arg.ParameterValueHi,
                    ParameterExpression.ConditionMode.LessThan);
            }

            var c1 = new ParameterExpression(arg.Parameter, arg.ParameterValueLo);
            var c2 = new ParameterExpression(arg.Parameter, arg.ParameterValueHi,
                ParameterExpression.ConditionMode.LessThan);
            return new AndNode(c1, c2);
        }

        private static string DescribeValue(object? value)
        {
            return value == null ? "null" : value.GetType().FullName;
        }

    }
}
