#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public class MotionNode : IMotionNode
    {
        public VirtualMotion Motion { get; }

        public MotionNode(VirtualMotion motion)
        {
            Motion = motion;
        }

        public VirtualMotion Bake(BakeContext context)
        {
            return Motion;
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
        }
    }
}
