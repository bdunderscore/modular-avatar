#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    internal class ParameterInfoRecord
    {
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public List<ParameterDefinition> WantedParameters { get; set; } = new();
        public List<ParameterDefinition> ActualParameters { get; set; } = new();

        public BuildTarget Target { get; set; } = BuildTarget.NoTarget;

        private sealed class ActualParametersEqualityComparer : IEqualityComparer<ParameterInfoRecord>
        {
            public bool Equals(ParameterInfoRecord x, ParameterInfoRecord y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null) return false;
                if (y is null) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.ActualParameters.SequenceEqual(y.ActualParameters, ParameterDefinition.CriticalValueComparer);
            }

            public int GetHashCode(ParameterInfoRecord obj)
            {
                // We're not going to be hashing with this comparer
                return 0;
            }
        }

        public static IEqualityComparer<ParameterInfoRecord> ActualParametersComparer { get; } =
            new ActualParametersEqualityComparer();
    }
}

#endif