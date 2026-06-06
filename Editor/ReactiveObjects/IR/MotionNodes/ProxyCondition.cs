#nullable enable

using System;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     Represents a condition via a IMotionNode which triggers a true/false animation or set of animations.
    ///     This can be used, for example, to use a BranchNode as a virtual boolean expression; at bake time, the containing
    ///     node will fill in the OnTrue or OnFalse branches, then bake the node to an actual Motion.
    /// </summary>
    public class ProxyCondition
    {
        // Private backing fields used for ref-passing
        private ProxyNode _onFalseProxy;
        private ProxyNode _onTrueProxy;
        private readonly ProxyNode _node = new();

        public bool InitialState { get; set; }

        public ProxyNode OnFalseProxy
        {
            get => _onFalseProxy;
            set => _onFalseProxy = value;
        }

        public IMotionNode OnFalse
        {
            get => RequiredTarget(OnFalseProxy, nameof(OnFalse));
            set => OnFalseProxy.Target = value;
        }

        public ProxyNode OnTrueProxy
        {
            get => _onTrueProxy;
            set => _onTrueProxy = value;
        }

        public IMotionNode OnTrue
        {
            get => RequiredTarget(OnTrueProxy, nameof(OnTrue));
            set => OnTrueProxy.Target = value;
        }

        public IMotionNode Node
        {
            get => RequiredTarget(_node, nameof(Node));
            set => _node.Target = value;
        }

        public IMotionNode ProxyNode => _node;
        
        public ProxyCondition(bool initialState, IMotionNode node, ProxyNode onFalse, ProxyNode onTrue)
        {
            InitialState = initialState;
            Node = node;
            _onFalseProxy = onFalse;
            _onTrueProxy = onTrue;
        }

        public static ProxyCondition FromInner(bool initialState, Func<ProxyNode, ProxyNode, IMotionNode> buildInner)
        {
            var onFalse = new ProxyNode(null);
            var onTrue = new ProxyNode(null);
            return new ProxyCondition(initialState, buildInner(onFalse, onTrue), onFalse, onTrue);
        }

        public static ProxyCondition Always()
        {
            var proxyNode = new ProxyNode(null);
            var onFalse = new ProxyNode();
            return new ProxyCondition(true, proxyNode, onFalse, proxyNode);
        }

        public IMotionNode Flatten(IMotionNode onFalse, IMotionNode onTrue)
        {
            _onFalseProxy.Target = onFalse;
            _onTrueProxy.Target = onTrue;

            _node.WalkTree(FlattenVisitor);

            return Node;

            void FlattenVisitor(ref IMotionNode node)
            {
                if (node == _onFalseProxy) node = RequiredTarget(_onFalseProxy, nameof(OnFalse));
                else if (node == _onTrueProxy) node = RequiredTarget(_onTrueProxy, nameof(OnTrue));
            }
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
            var target = Node;
            visitor(ref target);
            _node.Target = target;
        }

        private static IMotionNode RequiredTarget(ProxyNode proxy, string targetName)
        {
            return proxy.Target ?? throw new InvalidOperationException($"ProxyCondition {targetName} target is null");
        }
    }
}
