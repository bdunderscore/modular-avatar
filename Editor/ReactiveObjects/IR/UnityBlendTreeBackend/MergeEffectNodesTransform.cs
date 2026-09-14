#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    ///     This transformation identifies effect nodes with equivalent conditions, and merges their conditions
    /// </summary>
    internal class MergeEffectNodesTransform
    {
        public static List<EffectGroup> MergeNodes(UnityBlendTreeBackend backend, List<EffectGroup> effectGroups)
        {
            var newGroups = new List<EffectGroup>();
            foreach (var group in effectGroups.GroupBy(ConditionFingerprint))
            {
                if (group.Count() == 1)
                {
                    newGroups.Add(group.First());
                    continue;
                }

                var first = group.First();

                if (group.Any(g => g != first && !first.Processor.CanMerge(first, g)))
                {
                    // Processor rejects the merge
                    newGroups.AddRange(group);
                    continue;
                }

                var newGroup = EffectGroup.Merge(group.ToList());
                newGroups.Add(newGroup);
            }

            return newGroups;
        }

        private static Fingerprint ConditionFingerprint(EffectGroup arg)
        {
            return new Fingerprint(arg.Processor, arg.Nodes.Select(n => n.Expression).ToImmutableList());
        }

        private class Fingerprint : IEquatable<Fingerprint>
        {
            private readonly int hashCode;
            private readonly IEffectProcessor processor;
            private readonly ImmutableList<IExpression> expressions;

            public Fingerprint(IEffectProcessor processor, ImmutableList<IExpression> expressions)
            {
                this.processor = processor;
                this.expressions = expressions;
                hashCode = processor.GetHashCode();
                foreach (var expr in expressions)
                {
                    hashCode = HashCode.Combine(hashCode, expr);
                }
            }

            public bool Equals(Fingerprint other)
            {
                if (hashCode != other.hashCode) return false;
                if (!processor.Equals(other.processor)) return false;
                return expressions.SequenceEqual(other.expressions);
            }

            public override bool Equals(object obj)
            {
                return obj is Fingerprint other && Equals(other);
            }

            public override int GetHashCode()
            {
                return hashCode;
            }
        }
    }
}