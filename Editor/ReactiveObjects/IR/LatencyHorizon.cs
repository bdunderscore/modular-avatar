using System;
using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    public class LatencyHorizon : IConditionNode
    {
        public IConditionNode Downstream { get; set; }

        public LatencyHorizon(IConditionNode downstream)
        {
            Downstream = downstream;
        }

        public VirtualMotion Bake(BakeContext context)
        {
            return Downstream.Bake(context);
        }

        public void WalkTree(ConditionNodeVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}