#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Performs a prepass to identify and import external animations which influence game object active states.
    ///     This pass will add parameter curves to preexisting animations which manipulate game object active states,
    ///     and rewrite expressions to use these parameters. After completing this pass, all ObjectActiveState expressions
    ///     only need to consider effects from within the RC framework.
    ///     This pass also replaces undriven object state nodes with constant nodes.
    /// </summary>
    public static class ProcessExternalObjectStateInputsTransform
    {
        public static void Apply(ReactionGraph graph, BakeContext context)
        {
            Dictionary<GameObject, string?> objectToParameter = new();
            HashSet<GameObject> drivenObjects = new();

            foreach (var node in graph.Nodes)
            {
                foreach (var effect in node.Effects)
                {
                    if (effect is DriveActiveState drive)
                    {
                        drivenObjects.Add(drive.Target);
                    }
                }
            }

            // Track which objects are referenced by ObjectActiveState so we can seed base layer
            var referencedObjects = new HashSet<GameObject>();

            WalkAllExpressions(graph, WalkExpression);

            // After processing, add base-layer defaults for any object that had external clips and OAS usage
            foreach (var kvp in objectToParameter)
            {
                var obj = kvp.Key;
                var param = kvp.Value;
                if (param == null) continue; // only add defaults when we substituted a parameter
                if (!referencedObjects.Contains(obj)) continue; // only for objects referenced by OAS

                var path = context.ObjectPathRemapper.GetVirtualPathForObject(obj);

                context.BaseLayerClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1, obj.activeSelf ? 1 : 0)
                );

                context.BaseLayerClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), param),
                    AnimationCurve.Constant(0, 1, obj.activeSelf ? 1 : 0)
                );
            }

            void WalkExpression(ref IExpression expr)
            {
                if (expr is ObjectActiveState oas && oas.StateMode != ObjectActiveState.State.NotDriven)
                {
                    referencedObjects.Add(oas.TargetObject);

                    if (!objectToParameter.TryGetValue(oas.TargetObject, out var param))
                    {
                        param = MaybeSubstituteObject(context, oas.TargetObject);
                        objectToParameter[oas.TargetObject] = param;
                    }

                    IExpression baseNode;
                    if (param == null)
                    {
                        baseNode = new Constant(oas.TargetObject.activeSelf ==
                                                (oas.StateMode == ObjectActiveState.State.Active));
                    }
                    else
                    {
                        var wantActive = oas.StateMode == ObjectActiveState.State.Active;
                        baseNode = new ParameterExpression(param, 0.5f,
                            wantActive
                                ? ParameterExpression.ConditionMode.GreaterThan
                                : ParameterExpression.ConditionMode.LessThan);
                    }

                    var hasDriver = drivenObjects.Contains(oas.TargetObject);
                    IExpression notDriven = hasDriver
                        ? new ObjectActiveState(oas.TargetObject, ObjectActiveState.State.NotDriven)
                        : new Constant(true);
                    var driven = hasDriver ? expr : new Constant(false);

                    baseNode = new AndNode(baseNode, notDriven);

                    expr = new OrNode(baseNode, driven);
                }
                else
                {
                    // not an OAS node
                    expr.Walk(WalkExpression);
                }
            }
        }

        private static string? MaybeSubstituteObject(BakeContext context, GameObject target)
        {
            var objectPath = context.ObjectPathRemapper.GetVirtualPathForObject(target);
            var binding = EditorCurveBinding.FloatCurve(objectPath, typeof(GameObject), "m_IsActive");
            var clips = context.AnimationIndex.GetClipsForBinding(binding).ToList();

            if (clips.Count == 0)
            {
                return null;
            }

            var param = context.AddParameter("ActiveSelf", target.activeSelf ? 1 : 0);
            var paramBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), param);

            foreach (var clip in clips)
            {
                var curve = clip.GetFloatCurve(binding);

                // TODO adjust tangents to avoid float values here
                clip.SetFloatCurve(paramBinding, curve);
            }

            return param;
        }

        private static void WalkAllExpressions(ReactionGraph graph, ExpressionVisitor walk)
        {
            foreach (var node in graph.Nodes)
            {
                var tmp = node.Expression;
                walk(ref tmp);
                node.Expression = tmp;
            }
        }
    }
}