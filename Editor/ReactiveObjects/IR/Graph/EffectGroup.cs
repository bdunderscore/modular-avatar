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
        private EffectGroup(object targetKey, List<ReactionNode> nodes, List<ProxyCondition> proxyConditions,
            IMotionNode rootNode, int latency, int? depth, int? defaultNode)
        {
            TargetKey = targetKey;
            Nodes = nodes;
            _proxyConditions = proxyConditions;
            RootNode = rootNode;
            Latency = latency;
            Depth = depth;
            DefaultNode = defaultNode;
        }

        public EffectGroup(BakeContext context, object targetKey, List<ReactionNode> nodes)
        {
            TargetKey = targetKey;
            Nodes = nodes;
            Latency = nodes.Count > 2 ? 2 : 1;

            var conditions = new List<(ProxyCondition, IMotionNode)>();
            foreach (var node in nodes)
            {
                var effect = node.Effects.First(e => e.TargetKey.Equals(targetKey));
                var motion = VirtualClip.Create("Effect " + effect);
                effect.ToMotion(context, motion);

                var proxyCondition = ProxyCondition.Always();
                _proxyConditions.Add(proxyCondition);

                conditions.Add((proxyCondition, new MotionNode(motion)));
            }

            if (conditions.Count <= 2)
            {
                IMotionNode onFalse = new EmptyNode();
                for (var i = conditions.Count - 1; i >= 0; i--)
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
                RootNode = pn;
            }

            Latency = RootNode.Latency + 1;
        }

        public EffectGroup DeepClone()
        {
            var clonedNodes = Nodes.Select(n =>
            {
                var clone = new ReactionNode(n.Expression.DeepClone(), n.Effects[0]) { Priority = n.Priority };
                for (var i = 1; i < n.Effects.Count; i++) clone.Effects.Add(n.Effects[i]);
                return clone;
            }).ToList();

            var newPCs = _proxyConditions.Select(_ => ProxyCondition.Always()).ToList();
            IMotionNode newRoot;

            if (newPCs.Count <= 2)
            {
                IMotionNode onFalse = new EmptyNode();
                for (var i = newPCs.Count - 1; i >= 0; i--)
                {
                    newPCs[i].OnFalse = onFalse;
                    newPCs[i].OnTrue = _proxyConditions[i].OnTrue;
                    onFalse = newPCs[i].ProxyNode;
                }

                newRoot = onFalse;
            }
            else
            {
                var pn = new PriorityNode();
                pn.Conditions = newPCs.Select((pc, i) => (pc, _proxyConditions[i].OnTrue)).ToList();
                newRoot = pn;
            }

            return new EffectGroup(TargetKey, clonedNodes, newPCs, newRoot, Latency, Depth, DefaultNode);
        }

        public IMotionNode Emit()
        {
            // Now that we've finished any expression transformations, assign the proxy conditions
            foreach (var (node, proxy) in Nodes.Zip(_proxyConditions, (n, p) => (n, p)))
            {
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
                    return new BranchNode(ipc.ParameterName, onTrue, onFalse);
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

                    bn.Threshold = pe.Threshold;
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