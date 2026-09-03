#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    internal static class AssignInitialStates
    {
        public static void ProcessGraph(IReactionBackend backend, ReactionGraph graph)
        {
            Dictionary<ParameterTarget, bool> currentValues = new();
            Dictionary<ParameterTarget, bool> nextValues = new();

            var drivers = graph.Nodes
                .SelectMany(n =>
                    n.Effects.OfType<DriveInternalParameter>().Select(e => (expr: n.Expression, effect: e)))
                .ToList();

            if (drivers.Count == 0) return;

            Func<string, float> getParameterInitialValue = parameterName =>
                currentValues.TryGetValue(new ParameterTarget(parameterName), out var value)
                    ? (value ? 1.0f : 0.0f)
                    : backend.GetParameterInitialValue(parameterName);

            var iterationLimit = CalculateGraphDiameter(drivers) + 1;
            var changed = false;

            do
            {
                changed = false;

                foreach (var (expr, driver) in drivers)
                {
                    var target = new ParameterTarget(driver.ParameterName);
                    bool? state = null;

                    var exprValue = expr.Evaluate(getParameterInitialValue);
                    if (exprValue)
                    {
                        state = driver.State;
                    }

                    if (state.HasValue)
                    {
                        nextValues[target] = state.Value;
                    }
                }

                if (currentValues.Count != nextValues.Count)
                {
                    changed = true;
                }
                else
                {
                    foreach (var (k, v) in nextValues)
                    {
                        if (!currentValues.TryGetValue(k, out var currentValue) || currentValue != v)
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                currentValues = nextValues;
                nextValues = new Dictionary<ParameterTarget, bool>();
            } while (changed && --iterationLimit > 0);

            foreach (var (k, v) in currentValues)
            {
                backend.SetParameterInitialValue(k.ParameterName, v ? 1.0f : 0.0f);
            }
        }

        private static int CalculateGraphDiameter(List<(IExpression expr, DriveInternalParameter effect)> drivers)
        {
            var parameters = new HashSet<ParameterTarget>();
            var edges = new Dictionary<ParameterTarget, HashSet<ParameterTarget>>();

            foreach (var (expr, effect) in drivers)
            {
                var target = new ParameterTarget(effect.ParameterName);
                parameters.Add(target);
                AddDependencyEdges(expr, target, parameters, edges);
            }

            // Each pass advances state through one dependency edge. Limit the relaxation to one visit per parameter
            // so feedback cycles are bounded while every acyclic path can propagate to its terminal parameter.
            var distances = parameters.ToDictionary(parameter => parameter, _ => 0);
            for (var iteration = 1; iteration < parameters.Count; iteration++)
            {
                var nextDistances = new Dictionary<ParameterTarget, int>(distances);
                foreach (var (source, targets) in edges)
                {
                    foreach (var target in targets)
                    {
                        nextDistances[target] = System.Math.Max(nextDistances[target], distances[source] + 1);
                    }
                }

                distances = nextDistances;
            }

            return distances.Values.Max();
        }

        private static void AddDependencyEdges(
            IExpression expr,
            ParameterTarget target,
            HashSet<ParameterTarget> parameters,
            Dictionary<ParameterTarget, HashSet<ParameterTarget>> edges
        )
        {
            switch (expr)
            {
                case InternalParameterCondition condition:
                {
                    var source = new ParameterTarget(condition.ParameterName);
                    parameters.Add(source);
                    if (!edges.TryGetValue(source, out var targets))
                    {
                        targets = new HashSet<ParameterTarget>();
                        edges[source] = targets;
                    }

                    targets.Add(target);
                    break;
                }
                case AndNode and:
                    foreach (var child in and.Children)
                    {
                        AddDependencyEdges(child, target, parameters, edges);
                    }
                    break;
                case OrNode or:
                    foreach (var child in or.Children)
                    {
                        AddDependencyEdges(child, target, parameters, edges);
                    }
                    break;
                case NotNode not:
                    AddDependencyEdges(not.Inner, target, parameters, edges);
                    break;
            }
        }
    }
}
