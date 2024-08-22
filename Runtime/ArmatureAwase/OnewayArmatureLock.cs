#region

using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
#if UNITY_EDITOR
#endif

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class OnewayArmatureLockOperator : ArmatureLockOperator<OnewayArmatureLockOperator>
    {
        private NativeArrayRef<BoneStaticData> _boneStaticData;
        private NativeArrayRef<TransformState> _mergeSavedState;

        private List<(Transform, Transform)> _transforms;
        protected override bool WritesBaseBones => false;

        public OnewayArmatureLockOperator()
        {
            _boneStaticData = _memoryManager.CreateArray<BoneStaticData>();
            _mergeSavedState = _memoryManager.CreateArray<TransformState>();
        }

        protected override bool SetupJob(ISegment segment)
        {
            for (int i = 0; i < segment.Length; i++)
            {
                int bone = segment.Offset + i;

                var baseState = TransformState.FromTransform(BaseTransforms[bone]);
                var mergeState = TransformState.FromTransform(TargetTransforms[bone]);
                var baseParentState = TransformState.FromTransform(BaseTransforms[bone].parent);
                var mergeParentState = TransformState.FromTransform(TargetTransforms[bone].parent);

                if (!new ComputePosition().SyncState(out var staticData, baseState, mergeState, baseParentState,
                        mergeParentState))
                {
                    return false;
                }

                _boneStaticData.Array[bone] = staticData;
                _mergeSavedState.Array[bone] = mergeState;
            }

            return true;
        }

        protected override JobHandle Compute(ArmatureLockJobAccessor accessor, int? jobIndex, JobHandle dependency)
        {
            return new ComputePosition()
            {
                _baseState = accessor._in_baseBone,
                _mergeState = accessor._in_targetBone,
                _baseParentState = accessor._in_baseParentBone,
                _mergeParentState = accessor._in_targetParentBone,
                _mergeSavedState = _mergeSavedState,
                _boneStatic = _boneStaticData,
                _fault = accessor._abortFlag,
                _wroteAny = accessor._didAnyWriteFlag,
                _wroteBone = accessor._out_dirty_targetBone,
                jobIndexLimit = jobIndex ?? -1,
                _boneToJobIndex = accessor._boneToJobIndex,
                _outputState = accessor._out_targetBone,
                _boneInUse = accessor._in_boneInUse,
            }.Schedule(accessor._in_baseBone.Length, 32, dependency);
        }
        
        struct BoneStaticData
        {
            public Matrix4x4 _mat_l, _mat_r;
        }


        [BurstCompile]
        struct ComputePosition : IJobParallelFor
        {
            public NativeArray<BoneStaticData> _boneStatic;

            [ReadOnly] public NativeArray<TransformState> _mergeState;
            [ReadOnly] public NativeArray<TransformState> _baseState;

            [ReadOnly] public NativeArray<TransformState> _mergeParentState;
            [ReadOnly] public NativeArray<TransformState> _baseParentState;

            public NativeArray<TransformState> _mergeSavedState;
            public NativeArray<TransformState> _outputState;
            public NativeArray<bool> _wroteBone;

            public int jobIndexLimit;

            [ReadOnly] public NativeArray<int> _boneToJobIndex;
            [ReadOnly] public NativeArray<bool> _boneInUse;

            // job indexed
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public NativeArray<bool> _fault, _wroteAny;

            public bool SyncState(out BoneStaticData result, TransformState baseState, TransformState mergeState,
                TransformState baseParentState, TransformState mergeParentState)
            {
                if (SmallScale(mergeParentState.localScale) || SmallScale(mergeState.localScale) ||
                    SmallScale(baseState.localScale))
                {
                    result = default;
                    return false;
                }

                // We want to emulate the hierarchy:
                // baseParent
                //  - baseBone 
                //    - v_mergeBone 
                //
                // However our hierarchy actually is:
                // mergeParent
                //   - mergeBone
                //
                // Our question is: What is the local affine transform of mergeBone -> mergeParent space, given a new
                // baseBone -> baseParent affine transform?

                // First, relative to baseBone, what is the local affine transform of mergeBone?
                var mat_l = baseState.worldToLocalMatrix * mergeState.localToWorldMatrix;
                // We also find parent -> mergeParent
                var mat_r = mergeParentState.worldToLocalMatrix * baseParentState.localToWorldMatrix;
                // Now we can multiply:
                // (baseParent -> mergeParent) * (baseBone -> baseParent) * (mergeBone -> baseBone)
                //  = (baseParent -> mergeParent) * (mergeBone -> baseParent)
                //  = (mergeBone -> mergeParent)

                result = new BoneStaticData()
                {
                    _mat_l = mat_r,
                    _mat_r = mat_l
                };

                return true;
            }

            private bool SmallScale(Vector3 scale)
            {
                var epsilon = 0.000001f;

                return (scale.x < epsilon || scale.y < epsilon || scale.z < epsilon);
            }
            
            public void Execute(int index)
            {
                if (!_boneInUse[index]) return;
                _wroteBone[index] = false;
                
                var jobIndex = _boneToJobIndex[index];

                if (jobIndexLimit >= 0 && jobIndex >= jobIndexLimit) return;

                var boneStatic = _boneStatic[index];
                var mergeState = _mergeState[index];
                var baseState = _baseState[index];
                var mergeSaved = _mergeSavedState[index];

                var basePos = baseState.localPosition;
                var baseRot = baseState.localRotation;
                var baseScale = baseState.localScale;

                if (TransformState.Differs(mergeSaved, mergeState))
                {
                    // Reinitialize our transform matrices here, so we can continue to track on the next frame
                    if (SyncState(out var state,
                            _baseState[index],
                            _mergeState[index],
                            _baseParentState[index],
                            _mergeParentState[index]))
                    {
                        _boneStatic[index] = state;
                        _mergeSavedState[index] = mergeState;
                    }
                    else
                    {
                        _fault[jobIndex] = true;
                    }

                    return;
                }

                var relTransform = boneStatic._mat_l * Matrix4x4.TRS(basePos, baseRot, baseScale) * boneStatic._mat_r;

                var targetMergePos = relTransform.MultiplyPoint(Vector3.zero);
                var targetMergeRot = relTransform.rotation;
                var targetMergeScale = relTransform.lossyScale;

                var newState = new TransformState
                {
                    localPosition = targetMergePos,
                    localRotation = targetMergeRot,
                    localScale = targetMergeScale
                };

                if (TransformState.Differs(mergeSaved, newState))
                {
                    _wroteAny[jobIndex] = true;
                    _wroteBone[index] = true;
                    _mergeSavedState[index] = newState;
                    _outputState[index] = newState;
                }
            }
        }
    }
}