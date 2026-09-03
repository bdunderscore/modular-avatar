#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     An effect group represents a grouping of effects of which only one is actually applied (specifically, the last
    ///     one which is active in the list of ReactionNodes).
    /// </summary>
    internal class EffectGroup
    {
        public EffectGroup(UnityBlendTreeBackend context, object targetKey, List<ReactionNode> nodes)
        {
            TargetKey = targetKey;
            Nodes = nodes;
            Latency = nodes.Count > 2 ? 2 : 1;

            var conditions = new List<(ProxyCondition, IMotionNode)>();
            foreach (var node in nodes)
            {
                var effect = node.Effects.First(e => e.TargetKey.Equals(targetKey));
                var motion = context.EmitAction(effect);

                var proxyCondition = ProxyCondition.Always();
                _proxyConditions.Add(proxyCondition);

                conditions.Add((proxyCondition, motion));
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

                RootNode = onFalse;
            }
            else
            {
                var pn = new PriorityNode();
                pn.Conditions = conditions;
                // PriorityNode expects highest priority first
                pn.Conditions.Reverse();
                RootNode = pn;
            }

            Latency = RootNode.Latency + 1;
        }

        public IMotionNode Emit()
        {
            // Now that we've finished any expression transformations, assign the proxy conditions
            foreach (var (node, index) in Nodes.Select((node, index) => (node, index)))
            {
                var proxy = _proxyConditions[index];
                proxy.InitialState = DefaultNode == index;
                proxy.Node = EmitCondition(node.Expression, proxy.OnTrueProxy, proxy.OnFalseProxy);
            }

            return RootNode;
        }

        public readonly object TargetKey;
        public readonly List<ReactionNode> Nodes;
        private readonly List<ProxyCondition> _proxyConditions = new();

        public IMotionNode RootNode;

        /// <summary>
        ///     The number of frames between the inputs to this node, to the outputs of the node.
        /// </summary>
        public readonly int Latency;

        /// <summary>
        ///     Represents the number of frames away this node is from an externally-visible effect.
        ///     It follows that external effects always have depth zero.
        /// </summary>
        public int? Depth;

        public int? DefaultNode;


        private static IMotionNode EmitCondition(IExpression expr, IMotionNode onTrue, IMotionNode onFalse)
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

        public override string ToString()
        {
            return $"EffectGroup({TargetKey})";
        }
    }
}
