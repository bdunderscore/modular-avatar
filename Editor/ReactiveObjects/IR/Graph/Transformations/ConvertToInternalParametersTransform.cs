#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Converts all ObjectActiveState expressions into InternalParameterConditions, and all
    ///     ObjectActiveDriver nodes (DriveActiveState actions) into InternalParameterDrivers
    ///     (DriveInternalParameter actions). This is step 7 in the transformation pipeline.
    /// </summary>
    internal static class ConvertToInternalParametersTransform
    {
        public static void Apply(IReactionBackend backend, ReactionGraph graph)
        {
            // Map each object to a unique internal parameter name
            var objectToParameter = new Dictionary<GameObject, string>();
            var objectToDrivenParameter = new Dictionary<GameObject, string>();
            var driverConditions = new Dictionary<GameObject, List<IExpression>>();

            // First pass: create parameters for all objects that are either:
            // 1. Referenced in ObjectActiveState expressions
            // 2. Driven by DriveActiveState actions
            foreach (var node in graph.Nodes)
            {
                // Collect objects from expressions
                CollectObjectsFromExpression(node.Expression, objectToParameter, backend);

                // Collect objects from actions
                foreach (var effect in node.Effects)
                {
                    if (effect is DriveActiveState das)
                    {
                        EnsureParameter(das.Target, objectToParameter, backend);
                        EnsureDrivenParameter(das.Target, objectToDrivenParameter, backend);
                        if (!driverConditions.TryGetValue(das.Target, out var conditions))
                        {
                            conditions = new List<IExpression>();
                            driverConditions[das.Target] = conditions;
                        }
                        conditions.Add(node.Expression.DeepClone());
                    }
                }
            }

            // Second pass: replace expressions and actions
            foreach (var node in graph.Nodes)
            {
                // Replace ObjectActiveState expressions with InternalParameterConditions
                node.Expression = ReplaceObjectActiveStates(node.Expression, objectToParameter, objectToDrivenParameter);

                // Replace DriveActiveState actions with DriveInternalParameter actions
                var initialCount = node.Effects.Count;
                for (var i = 0; i < initialCount; i++)
                {
                    if (node.Effects[i] is DriveActiveState das)
                    {
                        var paramName = objectToParameter[das.Target];
                        node.Effects.Add(new DriveInternalParameter(paramName, das.Active));
                        node.Effects.Add(new DriveInternalParameter(objectToDrivenParameter[das.Target], true));
                    }
                }
            }

            foreach (var (obj, conditions) in driverConditions)
            {
                var resetCondition = ReplaceObjectActiveStates(
                    new NotNode(new OrNode(conditions.ToArray())),
                    objectToParameter,
                    objectToDrivenParameter);
                graph.Nodes.Add(new ReactionNode(
                    resetCondition,
                    new DriveInternalParameter(objectToDrivenParameter[obj], false)));
            }
        }

        private static void CollectObjectsFromExpression(
            IExpression expr,
            Dictionary<GameObject, string> objectToParameter,
            IReactionBackend backend)
        {
            void Visit(ref IExpression e)
            {
                if (e is ObjectActiveState oas)
                {
                    EnsureParameter(oas.TargetObject, objectToParameter, backend);
                }
                else
                {
                    e.Walk(Visit);
                }
            }

            var tmp = expr;
            Visit(ref tmp);
        }

        private static void EnsureParameter(
            GameObject obj,
            Dictionary<GameObject, string> objectToParameter,
            IReactionBackend backend)
        {
            if (!objectToParameter.ContainsKey(obj))
            {
                var paramName = backend.AddParameter($"ObjActive/{obj.name}", obj.activeSelf ? 1 : 0);
                objectToParameter[obj] = paramName;
            }
        }


        private static void EnsureDrivenParameter(
            GameObject obj,
            Dictionary<GameObject, string> objectToDrivenParameter,
            IReactionBackend backend)
        {
            if (!objectToDrivenParameter.ContainsKey(obj))
            {
                objectToDrivenParameter[obj] = backend.AddParameter($"ObjDriven/{obj.name}", 0);
            }
        }
        private static IExpression ReplaceObjectActiveStates(
            IExpression expr,
            Dictionary<GameObject, string> objectToParameter,
            Dictionary<GameObject, string> objectToDrivenParameter)
        {
            void Visit(ref IExpression e)
            {
                e.Walk(Visit);

                if (e is ObjectActiveState oas)
                {
                    var active = new InternalParameterCondition(objectToParameter[oas.TargetObject]);
                    e = oas.StateMode switch
                    {
                        ObjectActiveState.State.Active => active,
                        ObjectActiveState.State.Inactive => new NotNode(active),
                        ObjectActiveState.State.NotDriven => objectToDrivenParameter.TryGetValue(
                            oas.TargetObject, out var drivenParameter)
                            ? new NotNode(new InternalParameterCondition(drivenParameter))
                            : new Constant(true),
                        _ => e
                    };
                }
            }

            var tmp = expr;
            Visit(ref tmp);
            return tmp;
        }
    }
}