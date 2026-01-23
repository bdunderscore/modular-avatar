#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public class ProxyNode : IConditionNode
    {
        public ProxyNode(IConditionNode? target)
        {
            Target = target;
        }

        private IConditionNode? _target;

        public IConditionNode? Target
        {
            get => _target;
            set => _target = value;
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
            visitor(ref _target);
        }

        public int MaxLatency => Target?.MaxLatency ?? 0;

        public VirtualMotion Bake(BakeContext context)
        {
            return Target?.Bake(context) ?? context.EmptyMotion;
        }
    }
}