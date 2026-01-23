using System;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     Represents a condition via a IConditionNode which triggers a true/false animation or set of animations.
    ///     This can be used, for example, to use a BranchNode as a virtual boolean expression; at bake time, the containing
    ///     node will fill in the OnTrue or OnFalse branches, then bake the node to an actual Motion.
    /// </summary>
    public class ProxyCondition
    {
        // Private backing fields used for ref-passing
        private ProxyNode _onFalseProxy;
        private ProxyNode _onTrueProxy;
        private IConditionNode _node;

        public bool InitialState { get; set; }

        public ProxyNode OnFalseProxy
        {
            get => _onFalseProxy;
            set => _onFalseProxy = value;
        }

        public IConditionNode OnFalse
        {
            get => OnFalseProxy.Target;
            set => OnFalseProxy.Target = value;
        }

        public ProxyNode OnTrueProxy
        {
            get => _onTrueProxy;
            set => _onTrueProxy = value;
        }

        public IConditionNode OnTrue
        {
            get => OnTrueProxy.Target;
            set => OnTrueProxy.Target = value;
        }

        public IConditionNode Node
        {
            get => _node;
            set => _node = value;
        }

        public ProxyCondition(bool initialState, IConditionNode node, ProxyNode onFalse, ProxyNode onTrue)
        {
            InitialState = initialState;
            _node = node;
            _onFalseProxy = onFalse;
            _onTrueProxy = onTrue;
        }

        public static ProxyCondition FromInner(bool initialState, Func<ProxyNode, ProxyNode, IConditionNode> buildInner)
        {
            var onFalse = new ProxyNode(null);
            var onTrue = new ProxyNode(null);
            return new ProxyCondition(initialState, buildInner(onFalse, onTrue), onFalse, onTrue);
        }

        public static ProxyCondition Always()
        {
            var proxyNode = new ProxyNode(null);
            return new ProxyCondition(true, proxyNode, proxyNode, proxyNode);
        }

        public IConditionNode Flatten(IConditionNode onFalse, IConditionNode onTrue)
        {
            _onFalseProxy.Target = onFalse;
            _onTrueProxy.Target = onTrue;

            _node.WalkTree(Visitor);

            return _node;
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
            visitor(ref _node);
        }

        private bool Visitor(ref IConditionNode? node)
        {
            if (node == _onFalseProxy) node = _onFalseProxy.Target;
            else if (node == _onTrueProxy) node = _onTrueProxy.Target;

            return true;
        }
    }
}