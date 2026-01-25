#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    public class ReactionGraph
    {
        public List<ReactionNode> Nodes = new();

        internal void AddNode(ReactionNode node)
        {
            Nodes.Add(node);
        }

        private void ReassignPriorities()
        {
            for (var i = 0; i < Nodes.Count; i++)
            {
                Nodes[i].Priority = i;
            }
        }

        /// <summary>
        ///     Breaks down all multi-effect nodes into single-effect nodes
        /// </summary>
        public void Decompose()
        {
            Nodes = Nodes.SelectMany(n => n.Effects.Select(effect => new ReactionNode(n.Expression, effect)))
                .ToList();
            ReassignPriorities();
        }

        public Dictionary<object, List<ReactionNode>> NodesByTarget()
        {
            return Nodes.SelectMany(n => n.Effects.Select(e => (e.TargetKey, n)))
                .GroupBy(p => p.TargetKey)
                .Select(g => (g.Key, g.Select(p => p.n).ToList()))
                .ToDictionary(kv => kv.Key, kv => kv.Item2);
        }

        /// <summary>
        ///     Performs a prepass to identify and import external animations which influence game object active states.
        ///     This pass will add parameter curves to preexisting animations which manipulate game object active states,
        ///     and rewrite expressions to use these parameters. After completing this pass, all ObjectActiveState expressions
        ///     only need to consider effects from within the RC framework.
        ///
        ///     This pass also replaces undriven object state nodes with constant nodes.
        /// </summary>
        /// <param name="context"></param>
        public void ProcessExternalObjectStateInputs(BakeContext context)
        {
            Dictionary<GameObject, string?> objectToParameter = new();
            HashSet<GameObject> drivenObjects = new();

            foreach (var node in Nodes)
            {
                foreach (var effect in node.Effects)
                {
                    if (effect is DriveActiveState drive)
                    {
                        drivenObjects.Add(drive.Target);
                    }
                }
            }

            WalkAllExpressions(WalkExpression);

            void WalkExpression(ref IExpression expr)
            {
                if (expr is ObjectActiveState oas && oas.StateMode != ObjectActiveState.State.NotDriven)
                {
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

        private string? MaybeSubstituteObject(BakeContext context, GameObject target)
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

        private void WalkAllExpressions(ExpressionVisitor walk)
        {
            foreach (var node in Nodes)
            {
                var tmp = node.Expression;
                walk(ref tmp);
                node.Expression = tmp;
            }
        }

        /// <summary>
        ///     Simplifies boolean expressions by:
        ///     1. Removing constants inside AND and OR nodes
        ///     2. Collapsing AND-inside-AND and OR-inside-OR
        ///     3. Collapsing single-element AND and OR nodes
        ///     4. Collapsing nested NOT nodes, and constant-in-NOT nodes
        /// </summary>
        public void BooleanSimplify()
        {
            WalkAllExpressions(Walk);

            void Walk(ref IExpression expression)
            {
                expression.Walk(Walk);

                switch (expression)
                {
                    case AndNode and:
                    {
                        // Check for short-circuit (any false constant means the whole AND is false)
                        if (and.Children.Any(c => c is Constant constant && !constant.Value))
                        {
                            expression = new Constant(false);
                            break;
                        }

                        // Flatten nested ANDs and remove true constants
                        and.Children = and.Children.SelectMany(c =>
                        {
                            if (c is AndNode and2)
                            {
                                return (IEnumerable<IExpression>)and2.Children;
                            }

                            if (c is Constant c2 && c2.Value)
                            {
                                return Array.Empty<IExpression>();
                            }

                            return new[] { c };
                        }).ToList();

                        // After flattening/filtering, check if we can simplify further
                        if (and.Children.Count == 0)
                        {
                            expression = new Constant(true);
                        }
                        else if (and.Children.Count == 1)
                        {
                            expression = and.Children[0];
                        }

                        break;
                    }
                    case OrNode or:
                    {
                        // Check for short-circuit (any true constant means the whole OR is true)
                        if (or.Children.Any(c => c is Constant constant && constant.Value))
                        {
                            expression = new Constant(true);
                            break;
                        }

                        // Flatten nested ORs and remove false constants
                        or.Children = or.Children.SelectMany(c =>
                        {
                            if (c is OrNode or2)
                            {
                                return (IEnumerable<IExpression>)or2.Children;
                            }

                            if (c is Constant c2 && !c2.Value)
                            {
                                return Array.Empty<IExpression>();
                            }

                            return new[] { c };
                        }).ToList();

                        // After flattening/filtering, check if we can simplify further
                        if (or.Children.Count == 0)
                        {
                            expression = new Constant(true);
                        }
                        else if (or.Children.Count == 1)
                        {
                            expression = or.Children[0];
                        }

                        break;
                    }
                    case NotNode n:
                    {
                        if (n.Inner is Constant c)
                        {
                            expression = new Constant(!c.Value);
                        }
                        else if (n.Inner is NotNode n2)
                        {
                            expression = n2.Inner;
                        }

                        break;
                    }
                }
            }
        }
    }
}