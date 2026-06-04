#nullable enable

using System.Collections.Immutable;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     Implements a simple true/false branch
    /// </summary>
    public sealed class BranchNode : IMotionNode
    {
        public string Parameter { get; set; }
        public float Threshold = 0.99f;

        public IMotionNode OnGreaterEquals;
        public IMotionNode OnLessThan;

        public BranchNode(string parameterName, IMotionNode? onLess = null, IMotionNode? onGreaterEquals = null)
        {
            Parameter = parameterName;
            OnLessThan = onLess ?? EmptyNode.Instance;
            OnGreaterEquals = onGreaterEquals ?? EmptyNode.Instance;
        }

        public VirtualMotion Bake(BakeContext context)
        {
            var empty = context.EmptyMotion;

            var vbt = VirtualBlendTree.Create("BoolParam " + Parameter);

            var onLess = OnLessThan?.Bake(context) ?? empty;
            var onGreater = OnGreaterEquals?.Bake(context) ?? empty;

            vbt.BlendType = BlendTreeType.Simple1D;
            vbt.BlendParameter = Parameter;
            vbt.UseAutomaticThresholds = false;
            vbt.NormalizedBlendValues = false;
            vbt.Children = ImmutableList.Create(
                new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = onLess, Threshold = Threshold - 1f
                },
                // Ensure this is a hard transition by having no floats in between the false and true values
                new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = onLess, Threshold = Threshold.NextSmallest()
                },
                new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = onGreater, Threshold = Threshold
                },
                new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = onGreater, Threshold = Threshold + 1f
                }
            );

            return vbt;
        }

        public void WalkTree(MotionNodeVisitor visitor)
        {
            visitor(ref OnGreaterEquals);
            visitor(ref OnLessThan);
        }
    }
}
