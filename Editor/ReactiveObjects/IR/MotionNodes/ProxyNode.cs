#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public class ProxyNode : IMotionNode
    {
        public ProxyNode(IMotionNode? target)
        {
            Target = target;
        }

        private IMotionNode? _target;

        public IMotionNode? Target
        {
            get => _target;
            set => _target = value;
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
            visitor(ref _target);
        }

        public int Latency => Target?.Latency ?? 0;

        public VirtualMotion Bake(BakeContext context)
        {
            return Target?.Bake(context) ?? context.EmptyMotion;
        }
    }
}