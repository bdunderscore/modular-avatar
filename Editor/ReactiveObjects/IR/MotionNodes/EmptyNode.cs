#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal sealed class EmptyNode : IMotionNode
    {
        public static EmptyNode Instance = new();

        public VirtualMotion Bake(UnityBlendTreeBackend backend)
        {
            return backend.EmptyMotion;
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
        }
    }
}
