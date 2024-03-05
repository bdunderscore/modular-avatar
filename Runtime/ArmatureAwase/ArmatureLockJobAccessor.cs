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
        /// <summary>
        /// Initial transform states
        /// </summary>
        public NativeArray<TransformState> _in_baseBone, _in_targetBone, _in_baseParentBone, _in_targetParentBone;

        /// <summary>
        /// Transform states to write out (if _out_dirty is set)
        /// </summary>
        public NativeArray<TransformState> _out_baseBone, _out_targetBone;
        
        /// <summary>
        /// Flags indicating whether the given bone should be written back to its transform
        /// </summary>
        public NativeArray<bool> _out_dirty_baseBone, _out_dirty_targetBone;

        /// <summary>
        /// Indicates whether this bone index is associated with any job at all.
        /// </summary>
        [FormerlySerializedAs("_in_boneIsValid")]
        public NativeArray<bool> _in_boneInUse;
        
        /// <summary>
        /// Indexed by the job index (via _boneToJobIndex). If set to a nonzero value, none of the bones in this
        /// particular job (e.g. a single MergeArmature component) will be committed.
        ///
        /// Note: This array is written simultaneously from multiple threads. Jobs may set this to 1, but otherwise
        /// shouldn't read this value.
        /// </summary>
        [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
        public NativeArray<bool> _abortFlag;

        /// <summary>
        /// Indexed by the job index (via _boneToJobIndex). Should be set to a nonzero value when any bone in the job
        /// has changes that need to be written out.
        ///
        /// Note: This array is written simultaneously from multiple threads. Jobs may set this to 1, but otherwise
        /// shouldn't read this value.
        /// </summary>
        [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
        public NativeArray<bool> _didAnyWriteFlag;

        /// <summary>
        /// Maps from bone index to job index.
        /// </summary>
        [FormerlySerializedAs("_statusWordIndex")]
        public NativeArray<int> _boneToJobIndex;
    }
}