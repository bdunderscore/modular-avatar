#region

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Serialization;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    /// <summary>
    ///  Abstractly, an armature lock job works by taking the local transforms of the base armature and target armature,
    ///  deciding whether to abort updates, and if not, what the transforms should be set to, and writing out the
    ///  results.
    ///
    ///  This means that the interesting operation in the graph is one that takes as inputs:
    ///    NativeArray<TransformState> baseBonePositions
    ///    NativeArray<TransformState> targetBonePositions
    ///    NativeArray<int> statusWordPointer // which task is each bone associated with?
    ///  and has outputs:
    ///    NativeArray<TransformState> newBaseBonePositions
    ///    NativeArray<TransformState> newTargetBonePositions
    ///    NativeAtomicArray<int> statusWord  // array of status words for each task
    /// </summary>
    internal struct ArmatureLockJobAccessor
    {
        internal void Allocate(int nBones, int nWords)
        {
            _in_baseBone = new NativeArray<TransformState>(nBones, Allocator.Persistent);
            _in_targetBone = new NativeArray<TransformState>(nBones, Allocator.Persistent);
            _out_baseBone = new NativeArray<TransformState>(nBones, Allocator.Persistent);
            _out_targetBone = new NativeArray<TransformState>(nBones, Allocator.Persistent);
            _out_dirty_baseBone = new NativeArray<int>(nBones, Allocator.Persistent);
            _out_dirty_targetBone = new NativeArray<int>(nBones, Allocator.Persistent);
            _boneToJobIndex = new NativeArray<int>(nBones, Allocator.Persistent);
            _abortFlag = new NativeArray<int>(nWords, Allocator.Persistent);
            _didAnyWriteFlag = new NativeArray<int>(nWords, Allocator.Persistent);
        }

        internal void Destroy()
        {
            if (_in_baseBone.IsCreated) _in_baseBone.Dispose();
            _in_baseBone = default;
            if (_in_targetBone.IsCreated) _in_targetBone.Dispose();
            _in_targetBone = default;
            if (_out_baseBone.IsCreated) _out_baseBone.Dispose();
            _out_baseBone = default;
            if (_out_targetBone.IsCreated) _out_targetBone.Dispose();
            _out_targetBone = default;
            if (_out_dirty_baseBone.IsCreated) _out_dirty_baseBone.Dispose();
            _out_dirty_baseBone = default;
            if (_out_dirty_targetBone.IsCreated) _out_dirty_targetBone.Dispose();
            _out_dirty_targetBone = default;
            if (_boneToJobIndex.IsCreated) _boneToJobIndex.Dispose();
            _boneToJobIndex = default;
            if (_abortFlag.IsCreated) _abortFlag.Dispose();
            _abortFlag = default;
            if (_didAnyWriteFlag.IsCreated) _didAnyWriteFlag.Dispose();
            _didAnyWriteFlag = default;
        }

        public NativeArray<TransformState> _in_baseBone, _in_targetBone;

        public NativeArray<TransformState> _out_baseBone, _out_targetBone;
        public NativeArray<int> _out_dirty_baseBone, _out_dirty_targetBone;

        [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
        public NativeArray<int> _abortFlag;

        [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
        public NativeArray<int> _didAnyWriteFlag;

        [FormerlySerializedAs("_statusWordIndex")]
        public NativeArray<int> _boneToJobIndex;
    }
}