using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public class MotionNode : IMotionNode
    {
        private readonly VirtualMotion _motion;

        public MotionNode(VirtualMotion motion)
        {
            _motion = motion;
        }

        public VirtualMotion Bake(BakeContext context)
        {
            return _motion;
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
        }
    }
}