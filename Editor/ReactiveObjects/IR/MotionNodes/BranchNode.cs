#nullable enable

using System.Collections.Immutable;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     Implements a simple true/false branch
    /// </summary>
    internal sealed class BranchNode : IMotionNode
    {
        public string Parameter { get; set; }
        public float Threshold = 0.99f;

        public IMotionNode OnGreaterThan;
        public IMotionNode OnLessEquals;

        public BranchNode(string parameterName, IMotionNode? onLessEquals = null, IMotionNode? onGreater = null)
        {
            Parameter = parameterName;
            OnLessEquals = onLessEquals ?? EmptyNode.Instance;
            OnGreaterThan = onGreater ?? EmptyNode.Instance;
        }

        public VirtualMotion Bake(UnityBlendTreeBackend backend)
        {
            var empty = backend.EmptyMotion;

            var vbt = VirtualBlendTree.Create("BoolParam " + Parameter);

            var onLess = OnLessEquals?.Bake(backend) ?? empty;
            var onGreater = OnGreaterThan?.Bake(backend) ?? empty;

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
            visitor(ref OnGreaterThan);
            visitor(ref OnLessEquals);
        }
    }
}
