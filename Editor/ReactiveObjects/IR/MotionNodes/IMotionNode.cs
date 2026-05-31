#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public delegate void MotionNodeVisitor(ref IMotionNode node);

    public interface IMotionNode
    {
        /// <summary>
        ///     Returns the maximum amount of latency introduced between this node and any downstream nodes.
        ///     Zero means that we take effect the frame after our conditions change.
        /// </summary>
        public virtual int Latency => 0;

        public VirtualMotion Bake(BakeContext context);

        public void WalkTree(MotionNodeVisitor visitor);
    }
}