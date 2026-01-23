#nullable enable

using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public delegate bool ConditionNodeVisitor(ref IConditionNode node);

    public interface IConditionNode
    {
        /// <summary>
        ///     Returns the maximum amount of latency introduced between this node and any downstream nodes.
        /// </summary>
        public virtual int MaxLatency => 0;

        public VirtualMotion Bake(BakeContext context);

        public void WalkTree(ConditionNodeVisitor visitor);
    }
}