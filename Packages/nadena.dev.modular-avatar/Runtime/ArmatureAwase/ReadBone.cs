using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    [BurstCompile]
    internal struct ReadBone : IJobParallelForTransform
    {
        public NativeArray<TransformState> _state;

        public void Execute(int index, TransformAccess transform)
        {
            _state[index] = new TransformState
            {
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale
            };
        }
    }
}