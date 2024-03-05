#region

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class BidirectionalArmatureLockOperator : ArmatureLockOperator<BidirectionalArmatureLockOperator>
    {
        private NativeArrayRef<TransformState> SavedState;
        protected override bool WritesBaseBones => true;

        public BidirectionalArmatureLockOperator()
        {
            SavedState = _memoryManager.CreateArray<TransformState>();
        }

        protected override bool SetupJob(ISegment segment)
        {
            for (int i = 0; i < segment.Length; i++)
            {
                int bone = i + segment.Offset;

                var baseBone = BaseTransforms[bone];
                var targetBone = TargetTransforms[bone];

                SavedState.Array[i] = TransformState.FromTransform(targetBone);
                if (TransformState.Differs(TransformState.FromTransform(baseBone), SavedState.Array[i]))
                {
                    return false;
                }
            }

            return true;
        }

        protected override JobHandle Compute(ArmatureLockJobAccessor accessor, int? jobIndex, JobHandle dependency)
        {
            return new ComputeOperator()
            {
                base_in = accessor._in_baseBone,
                merge_in = accessor._in_targetBone,
                base_out = accessor._out_baseBone,
                merge_out = accessor._out_targetBone,

                SavedState = SavedState,
                baseDirty = accessor._out_dirty_baseBone,
                mergeDirty = accessor._out_dirty_targetBone,
                boneToJobIndex = accessor._boneToJobIndex,
                wroteAny = accessor._didAnyWriteFlag,

                boneInUse = accessor._in_boneInUse,

                singleJobIndex = jobIndex ?? -1
            }.Schedule(accessor._in_baseBone.Length, 16, dependency);
        }

        [BurstCompile]
        private struct ComputeOperator : IJobParallelFor
        {
            public int singleJobIndex;

            public NativeArray<TransformState> base_in, merge_in, base_out, merge_out;

            public NativeArray<TransformState> SavedState;

            [WriteOnly] public NativeArray<bool> baseDirty, mergeDirty;
            [ReadOnly] public NativeArray<int> boneToJobIndex;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] [WriteOnly]
            public NativeArray<bool> wroteAny;

            [ReadOnly] public NativeArray<bool> boneInUse;

            [BurstCompile]
            public void Execute(int index)
            {
                if (!boneInUse[index]) return;
                
                var jobIndex = boneToJobIndex[index];

                if (singleJobIndex != -1 && jobIndex != singleJobIndex) return;

                var baseBone = base_in[index];
                var mergeBone = merge_in[index];
                var saved = SavedState[index];

                if (TransformState.Differs(saved, mergeBone))
                {
                    baseDirty[index] = true;
                    mergeDirty[index] = false;

                    SavedState[index] = base_out[index] = merge_in[index];

                    wroteAny[jobIndex] = true;
                }
                else if (TransformState.Differs(saved, baseBone))
                {
                    mergeDirty[index] = true;
                    baseDirty[index] = false;

                    SavedState[index] = merge_out[index] = base_in[index];

                    wroteAny[jobIndex] = true;
                }
            }
        }
    }
}