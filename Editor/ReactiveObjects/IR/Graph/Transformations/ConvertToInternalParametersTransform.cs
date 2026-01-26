#nullable enable

using System.Collections.Generic;
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
    public static class ConvertToInternalParametersTransform
    {
        public static void Apply(ReactionGraph graph, BakeContext context)
        {
            // Map each object to a unique internal parameter name
            var objectToParameter = new Dictionary<GameObject, string>();

            // First pass: create parameters for all objects that are either:
            // 1. Referenced in ObjectActiveState expressions
            // 2. Driven by DriveActiveState actions
            foreach (var node in graph.Nodes)
            {
                // Collect objects from expressions
                CollectObjectsFromExpression(node.Expression, objectToParameter, context);

                // Collect objects from actions
                foreach (var effect in node.Effects)
                {
                    if (effect is DriveActiveState das)
                    {
                        EnsureParameter(das.Target, objectToParameter, context);
                    }
                }
            }

            // Second pass: replace expressions and actions
            foreach (var node in graph.Nodes)
            {
                // Replace ObjectActiveState expressions with InternalParameterConditions
                node.Expression = ReplaceObjectActiveStates(node.Expression, objectToParameter);

                // Replace DriveActiveState actions with DriveInternalParameter actions
                for (var i = 0; i < node.Effects.Count; i++)
                {
                    if (node.Effects[i] is DriveActiveState das)
                    {
                        var paramName = objectToParameter[das.Target];
                        node.Effects[i] = new DriveInternalParameter(paramName, das.Active);
                    }
                }
            }
        }

        private static void CollectObjectsFromExpression(
            IExpression expr,
            Dictionary<GameObject, string> objectToParameter,
            BakeContext context)
        {
            void Visit(ref IExpression e)
            {
                if (e is ObjectActiveState oas)
                {
                    EnsureParameter(oas.TargetObject, objectToParameter, context);
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
            BakeContext context)
        {
            if (!objectToParameter.ContainsKey(obj))
            {
                var paramName = context.AddParameter($"ObjActive/{obj.name}", obj.activeSelf ? 1 : 0);
                objectToParameter[obj] = paramName;
            }
        }

        private static IExpression ReplaceObjectActiveStates(
            IExpression expr,
            Dictionary<GameObject, string> objectToParameter)
        {
            void Visit(ref IExpression e)
            {
                e.Walk(Visit);

                if (e is ObjectActiveState oas)
                {
                    var paramName = objectToParameter[oas.TargetObject];
                    e = new InternalParameterCondition(paramName);
                }
            }

            var tmp = expr;
            Visit(ref tmp);
            return tmp;
        }
    }
}