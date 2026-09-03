#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal class MotionNode : IMotionNode
    {
        public VirtualMotion Motion { get; }

        public MotionNode(VirtualMotion motion)
        {
            Motion = motion;
        }

        public VirtualMotion Bake(UnityBlendTreeBackend backend)
        {
            return Motion;
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
        }
    }
}
