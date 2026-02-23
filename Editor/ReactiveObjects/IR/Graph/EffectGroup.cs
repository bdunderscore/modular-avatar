using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal class EffectGroup
    {
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

        private List<(ProxyNode, ProxyNode, ProxyNode)> _deferredExpressions = new();

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
                    return new BranchNode(pe.ParameterName, onTrue, onFalse);
                default:
                    throw new Exception($"Unhandled expression type {expr.GetType()}");
            }
        }
    }
}