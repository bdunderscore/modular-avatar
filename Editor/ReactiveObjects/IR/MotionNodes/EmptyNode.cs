#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public sealed class EmptyNode : IMotionNode
    {
        public static EmptyNode Instance = new();

        public VirtualMotion Bake(BakeContext context)
        {
            return context.EmptyMotion;
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
        }
    }
}
