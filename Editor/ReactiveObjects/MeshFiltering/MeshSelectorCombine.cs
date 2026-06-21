using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Shared implementation for <see cref="ANDFilter" /> and <see cref="ORFilter" />.
    ///     Each child selector is run into its own destination buffer, and the per-child results are reduced into the
    ///     caller's output using pairwise merge jobs. We cannot have the children write into disjoint slices of a single
    ///     shared NativeArray: Unity's job safety system tracks write access at the whole-container level, so scheduling
    ///     multiple jobs that each write to a slice of one array trips an aliasing conflict.
    /// </summary>
    internal static class MeshSelectorCombine
    {
        public static JobHandle Combine(
            bool isAnd,
            List<IMeshSelector> filters,
            MeshSelectorJob job,
            int submesh,
            NativeSlice<bool> output
        )
        {
            if (filters.Count == 0) return default;
            if (filters.Count == 1) return filters[0].MarkFilteredPrimitives(job, submesh, output);

            var length = output.Length;

            // Run each child into a buffer it exclusively owns.
            var pending = new List<(NativeArray<bool> arr, JobHandle handle)>(filters.Count);
            for (var i = 0; i < filters.Count; i++)
            {
                var dest = new NativeArray<bool>(length, Allocator.TempJob);
                var handle = filters[i].MarkFilteredPrimitives(job, submesh, dest);
                pending.Add((dest, handle));
            }

            var tmp = new NativeArray<bool>(length, Allocator.TempJob);
            JobHandle accumHandle = default;
            if (isAnd)
            {
                accumHandle = new FillArrayJob { Array = tmp, Value = true }.Schedule(length, 64);
            }

            for (var i = 1; i < pending.Count; i++)
            {
                var (arr, handle) = pending[i];
                var dependency = JobHandle.CombineDependencies(handle, accumHandle);
                if (isAnd)
                {
                    accumHandle = new MergeAndJob
                    {
                        Accum = tmp, Input = arr
                    }.Schedule(length, 64, dependency);
                }
                else
                {
                    accumHandle = new MergeOrJob
                    {
                        Accum = tmp, Input = arr
                    }.Schedule(length, 64, dependency);
                }
            }

            {
                var (a, ha) = pending[0];
                return new MergeIntoSliceJob
                {
                    A = a, B = tmp, Out = output, IsAnd = isAnd
                }.Schedule(length, 64, JobHandle.CombineDependencies(ha, accumHandle));
            }
        }

        [BurstCompile]
        private struct FillArrayJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<bool> Array;
            public bool Value;

            public void Execute(int index)
            {
                Array[index] = Value;
            }
        }

        [BurstCompile]
        private struct MergeAndJob : IJobParallelFor
        {
            public NativeArray<bool> Accum;
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<bool> Input;

            public void Execute(int index)
            {
                Accum[index] = Accum[index] && Input[index];
            }
        }

        [BurstCompile]
        private struct MergeOrJob : IJobParallelFor
        {
            public NativeArray<bool> Accum;
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<bool> Input;

            public void Execute(int index)
            {
                Accum[index] = Accum[index] || Input[index];
            }
        }

        [BurstCompile]
        private struct MergeIntoSliceJob : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<bool> A;
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<bool> B;

            [NativeDisableParallelForRestriction] [WriteOnly]
            public NativeSlice<bool> Out;

            public bool IsAnd;

            public void Execute(int index)
            {
                var result = IsAnd ? A[index] && B[index] : A[index] || B[index];
                if (result) Out[index] = true;
            }
        }
    }
}