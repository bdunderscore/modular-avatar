#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal delegate void MotionNodeVisitor(ref IMotionNode node);

    internal interface IMotionNode
    {
        /// <summary>Returns the maximum latency between this node and its downstream nodes.</summary>
        public virtual int Latency => 0;
        public VirtualMotion Bake(UnityBlendTreeBackend backend);
        public void WalkTree(MotionNodeVisitor visitor);
    }
}
