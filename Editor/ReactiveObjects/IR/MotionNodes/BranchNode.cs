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
                // ParameterExpression uses the same strict Greater semantics as AnimatorConditionMode.Greater.
                // Put the false sample at the threshold and the true sample at the next representable float so
                // equality remains false while there are no float values between the two samples to interpolate.
                new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = onLess, Threshold = Threshold
                },
                new VirtualBlendTree.VirtualChildMotion
                {
                    Motion = onGreater, Threshold = Threshold.NextLargest()
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
