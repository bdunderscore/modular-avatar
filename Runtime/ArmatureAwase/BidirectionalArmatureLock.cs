#region

using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class BidirectionalArmatureLockOperator : ArmatureLockOperator<BidirectionalArmatureLockOperator>
    {
        private NativeArray<TransformState> SavedState;
        protected override bool WritesBaseBones => true;

        protected override void Reinit(List<(Transform, Transform)> transforms, List<int> problems)
        {
            if (SavedState.IsCreated) SavedState.Dispose();

            SavedState = new NativeArray<TransformState>(transforms.Count, Allocator.Persistent);

            for (int i = 0; i < transforms.Count; i++)
            {
                var (baseBone, mergeBone) = transforms[i];
                SavedState[i] = TransformState.FromTransform(mergeBone);

                if (TransformState.Differs(TransformState.FromTransform(baseBone), SavedState[i]))
                {
                    problems.Add(i);
                }
            }
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

                singleJobIndex = jobIndex ?? -1
            }.Schedule(accessor._in_baseBone.Length, 16, dependency);
        }

        protected override void DerivedDispose()
        {
            SavedState.Dispose();
        }

        [BurstCompile]
        private struct ComputeOperator : IJobParallelFor
        {
            public int singleJobIndex;

            public NativeArray<TransformState> base_in, merge_in, base_out, merge_out;

            public NativeArray<TransformState> SavedState;

            [WriteOnly] public NativeArray<int> baseDirty, mergeDirty;
            [ReadOnly] public NativeArray<int> boneToJobIndex;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] [WriteOnly]
            public NativeArray<int> wroteAny;

            [BurstCompile]
            public void Execute(int index)
            {
                var jobIndex = boneToJobIndex[index];

                if (singleJobIndex != -1 && jobIndex != singleJobIndex) return;

                var baseBone = base_in[index];
                var mergeBone = merge_in[index];
                var saved = SavedState[index];

                if (TransformState.Differs(saved, mergeBone))
                {
                    baseDirty[index] = 1;
                    mergeDirty[index] = 0;

                    SavedState[index] = base_out[index] = merge_in[index];

                    wroteAny[jobIndex] = 1;
                }
                else if (TransformState.Differs(saved, baseBone))
                {
                    mergeDirty[index] = 1;
                    baseDirty[index] = 0;

                    SavedState[index] = merge_out[index] = base_in[index];

                    wroteAny[jobIndex] = 1;
                }
            }
        }
    }
}