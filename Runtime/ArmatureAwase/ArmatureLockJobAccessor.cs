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
    ///  This struct handles these common inputs and outputs for different armature lock types.
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

        /// <summary>
        /// Initial transform states
        /// </summary>
        public NativeArray<TransformState> _in_baseBone, _in_targetBone;

        /// <summary>
        /// Transform states to write out (if _out_dirty is set)
        /// </summary>
        public NativeArray<TransformState> _out_baseBone, _out_targetBone;
        
        /// <summary>
        /// Flags indicating whether the given bone should be written back to its transform
        /// </summary>
        public NativeArray<int> _out_dirty_baseBone, _out_dirty_targetBone;

        /// <summary>
        /// Indexed by the job index (via _boneToJobIndex). If set to a nonzero value, none of the bones in this
        /// particular job (e.g. a single MergeArmature component) will be committed.
        ///
        /// Note: This array is written simultaneously from multiple threads. Jobs may set this to 1, but otherwise
        /// shouldn't read this value.
        /// </summary>
        [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
        public NativeArray<int> _abortFlag;

        /// <summary>
        /// Indexed by the job index (via _boneToJobIndex). Should be set to a nonzero value when any bone in the job
        /// has changes that need to be written out.
        ///
        /// Note: This array is written simultaneously from multiple threads. Jobs may set this to 1, but otherwise
        /// shouldn't read this value.
        /// </summary>
        [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
        public NativeArray<int> _didAnyWriteFlag;

        /// <summary>
        /// Maps from bone index to job index.
        /// </summary>
        [FormerlySerializedAs("_statusWordIndex")]
        public NativeArray<int> _boneToJobIndex;
    }
}