#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal interface IEffectProcessor
    {
        /// <summary>
        ///     Determine if this group of same-key nodes can be processed by this EffectProcessor.
        /// </summary>
        /// <param name="targetKey"></param>
        /// <param name="nodes"></param>
        /// <returns>the effect group, if accepted, or null</returns>
        public EffectGroup? Accept(object targetKey,
            IReadOnlyList<ReactionNode> nodes);

        /// <summary>
        ///     Determines if it is safe to merge two effect groups together. The caller is responsible for
        ///     ensuring that both groups have the same conditions, effect processor, and latency.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public bool CanMerge(
            EffectGroup a,
            EffectGroup b
        );

        /// <summary>
        ///     Processes an effect group, adding it to the animator.
        /// </summary>
        /// <param name="group"></param>
        /// <param name="backend"></param>
        /// <returns>
        ///     The root IMotionNode (if eligible for condition merging). May return null if it handles insertion into the
        ///     animator itself
        /// </returns>
        public IMotionNode? ProcessEffectGroup(
            UnityBlendTreeBackend backend,
            EffectGroup group
        );
    }
}