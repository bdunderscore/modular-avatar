#if MA_VRCSDK3_AVATARS

#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    internal class AvatarRecord
    {
        public ParameterInfoRecord? PrimaryPlatformRecord { get; set; }
        public List<ParameterInfoRecord> SecondaryPlatformRecords { get; set; } = new();

        public bool IsConsistent
        {
            get
            {
                return PrimaryPlatformRecord?.ActualParameters != null && SecondaryPlatformRecords.All(pr =>
                    pr.ActualParameters.SequenceEqual(PrimaryPlatformRecord.ActualParameters,
                        ParameterDefinition.CriticalValueComparer)
                );
            }
        }
    }
}

#endif