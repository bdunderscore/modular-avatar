#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    /// <summary>
    ///     Simplifies boolean expressions by:
    ///     1. Removing constants inside AND and OR nodes
    ///     2. Collapsing AND-inside-AND and OR-inside-OR
    ///     3. Collapsing single-element AND and OR nodes
    ///     4. Collapsing nested NOT nodes, and constant-in-NOT nodes
    /// </summary>
    public static class BooleanSimplifyTransform
    {
        public static void Apply(ReactionGraph graph)
        {
            WalkAllExpressions(graph, Walk);

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

                        PruneIdenticalConditions(and.Children);
                        
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

                        PruneIdenticalConditions(or.Children);
                        
                        // After flattening/filtering, check if we can simplify further
                        if (or.Children.Count == 0)
                        {
                            expression = new Constant(false);
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

        /// <summary>
        ///     Prunes identical conditions in AND/OR nodes. This optimization is performance-critical - we frequently
        ///     generate duplicate parameter conditions, and allowing them to flow through the graph roughly doubles (or
        ///     more) the number of active blend trees.
        /// </summary>
        /// <param name="expressions"></param>
        private static void PruneIdenticalConditions(List<IExpression> expressions)
        {
            HashSet<IExpression> seen = new();

            expressions.RemoveAll(item => !seen.Add(item));
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