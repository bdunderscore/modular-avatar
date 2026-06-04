#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactiveObjectPassV2
    {
        private readonly ndmf.BuildContext context;
        private readonly AnimatorServicesContext asc;
        private BakeContext? _bakeContext;
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

            // Drop constant shapes that have no preexisting foreign animations (apply their value
            // directly to the scene object). Shapes that DO have foreign animations must be kept so
            // the apply layer can override them.
#if MA_VRCSDK3_AVATARS
            RemoveRedundantConstantShapes(shapes, initialStates);
#endif

            var hasAnimatableShapes = shapes.Values.Any(p => p.actionGroups.Count > 0);

#if MA_VRCSDK3_AVATARS
            if (hasAnimatableShapes)
            {
                var controller = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
                _bakeContext = new BakeContext(context, controller);
                ILBuild.Build(_bakeContext, ShapeToGraph(shapes));
            }

            // Apply the initially-active state to scene objects for all remaining (non-constant)
            // props. This must run after ILBuild so that SetBaseState has already read the original
            // scene values into BaseLayerClip before we overwrite them here.
            ApplyInitialSceneStates(shapes, initialStates);
#endif

            ApplyStaticStateOverrides(shapes);
        }

#if MA_VRCSDK3_AVATARS
        private void RemoveRedundantConstantShapes(
            Dictionary<TargetProp, AnimatedProperty> shapes,
            Dictionary<TargetProp, object> initialStates)
        {
            var constantShapes = shapes
                .Where(kv => kv.Value.actionGroups.LastOrDefault()?.IsConstant is true)
                .Where(kv => kv.Value.actionGroups.All(x => x.Value is not IVertexFilter))
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

                var ecb = EditorCurveBinding.FloatCurve(
                    asc.ObjectPathRemapper.GetVirtualPathForObject(gameObject),
                    key.TargetObject.GetType(),
                    key.PropertyName
                );

                // If any preexisting clip already animates this binding, keep the shape so the
                // apply layer can override it.
                if (asc.AnimationIndex.GetClipsForBinding(ecb).Any()) continue;

                shapes.Remove(key);

                // Apply the constant value directly to the scene object since no animation will.
                if (!initialStates.TryGetValue(key, out var constantValue) || constantValue == null)
                    continue;

                ApplyValueToSceneObject(key, constantValue);
            }
        }

        private static void ApplyInitialSceneStates(
            Dictionary<TargetProp, AnimatedProperty> shapes,
            Dictionary<TargetProp, object> initialStates)
        {
            foreach (var (key, prop) in shapes)
            {
                if (!prop.actionGroups.Any(ag => ag.InitiallyActive)) continue;
                if (!initialStates.TryGetValue(key, out var value) || value == null) continue;
                ApplyValueToSceneObject(key, value);
            }
        }

        private static void ApplyValueToSceneObject(TargetProp key, object value)
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
                    sprop.objectReferenceValue = (Object)value;
                    break;
                default:
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
#endif

        private void ApplyStaticStateOverrides(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
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

                // For no-action-group shapes, SetBaseState was never called, so we must record
                // the original value in BaseLayerClip so animations restore it when the object
                // is active (e.g., AudioSource re-enabled when its parent GameObject is toggled on).
                if (prop.actionGroups.Count == 0 && _bakeContext != null &&
                    key.TargetObject is Component c)
                {
                    _bakeContext.BaseLayerClip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(
                            _bakeContext.ObjectPathRemapper.GetVirtualPathForObject(c.gameObject),
                            key.TargetObject.GetType(),
                            key.PropertyName
                        ),
                        AnimationCurve.Constant(0, 1, originalValue)
                    );
                }
            }
        }

        private void PreProcessMeshDeletion(
            Dictionary<TargetProp, AnimatedProperty> shapes,
            Dictionary<TargetProp, object> initialStates)
        {
            var rendererGroups = shapes.Values
                .Where(prop => prop.actionGroups.Any(x => x.Value is IVertexFilter))
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

        private static IVertexFilter AggregateVertexFilters(IEnumerable<IVertexFilter?> filters)
        {
            var list = filters.ToList();
            var filter = list.LastOrDefault(f => f != null);
            if (filter is VertexFilterByShape filterByShape)
            {
                return new VertexFilterByShape(filterByShape.Shapes, list
                    .OfType<VertexFilterByShape>()
                    .Min(x => x.Threshold));
            }

            return filter!;
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
                        action = new DriveActiveState(go, (float)rule.Value! > 0.5f);
                    }
                    else if (_nanBonesForProp.TryGetValue(rule.TargetProp, out var bones))
                    {
                        if (rule.Value is IVertexFilter)
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
                    else if (rule.Value is IVertexFilter)
                    {
                        // No bones were generated (no-op filter or unconditional deletion already
                        // handled in pre-processing); skip this rule.
                        continue;
                    }
                    else
                    {
                        action = new PropAction(rule.TargetProp, rule.Value);
                    }

                    var conditions = rule.ControllingConditions.Select(ConvertCondition).ToArray();
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

        private IExpression ConvertCondition(ControlCondition arg)
        {
            if (arg.ReferenceObject != null)
            {
                return new ObjectActiveState(arg.ReferenceObject, ObjectActiveState.State.Active);
            }

            // TODO - find correct initial value here
            _bakeContext.EnsureParameterPresent(arg.Parameter);

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
    }
}
