using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class EffectProcessorHelpers
    {
        internal static int BranchLatency(int nodeCount)
        {
            return nodeCount <= 2 ? 1 : 2;
        }

        internal static IMotionNode GenerateBranch(
            IEnumerable<(IExpression condition, IMotionNode node)> nodes,
            int? defaultNode
        )
        {
            var conditions = new List<(ProxyCondition, IMotionNode)>();
            foreach (var ((condition, node), index) in nodes.Select((n, i) => (n, i)))
            {
                var proxyCondition = ProxyCondition.Always();
                proxyCondition.Node = EmitCondition(condition, proxyCondition.OnTrueProxy, proxyCondition.OnFalseProxy);
                proxyCondition.InitialState = index == defaultNode;

                conditions.Add((proxyCondition, node));
            }

            if (conditions.Count <= 2)
            {
                IMotionNode onFalse = new EmptyNode();
                for (var i = 0; i < conditions.Count; i++)
                {
                    var (pc, node) = conditions[i];
                    pc.OnFalse = onFalse;
                    pc.OnTrue = node;
                    onFalse = pc.ProxyNode;
                }

                return onFalse;
            }

            var pn = new PriorityNode();
            pn.Conditions = conditions;
            // PriorityNode expects highest priority first
            pn.Conditions.Reverse();
            return pn;
        }

        internal static IMotionNode EmitCondition(IExpression expr, IMotionNode onTrue, IMotionNode onFalse)
        {
            switch (expr)
            {
                case Constant c:
                    return c.Value ? onTrue : onFalse;
                case NotNode not:
                    return EmitCondition(not.Inner, onFalse, onTrue);
                case OrNode or:
                {
                    foreach (var child in or.Children)
                    {
                        onFalse = EmitCondition(child, onTrue, onFalse);
                    }

                    return onFalse;
                }
                case AndNode and:
                {
                    foreach (var child in and.Children)
                    {
                        onTrue = EmitCondition(child, onTrue, onFalse);
                    }

                    return onTrue;
                }
                case InternalParameterCondition ipc:
                    return new BranchNode(ipc.ParameterName, onFalse, onTrue);
                case ParameterExpression pe:
                {
                    BranchNode bn;
                    if (pe.Mode == ParameterExpression.ConditionMode.LessThan)
                    {
                        bn = new BranchNode(pe.ParameterName, onTrue, onFalse);
                    }
                    else
                    {
                        bn = new BranchNode(pe.ParameterName, onFalse, onTrue);
                    }

                    bn.Threshold = pe.Mode == ParameterExpression.ConditionMode.LessThan
                        ? pe.Threshold.NextSmallest()
                        : pe.Threshold;
                    return bn;
                }
                default:
                    throw new Exception($"Unhandled expression type {expr.GetType()}");
            }
        }
    }
}