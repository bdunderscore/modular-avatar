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
        private Transform[] _baseBones, _mergeBones, _baseParentBones, _mergeParentBones;
        private NativeArray<BoneStaticData> _boneStaticData;
        public NativeArray<TransformState> _mergeSavedState;

        private List<(Transform, Transform)> _transforms;
        protected override bool WritesBaseBones => false;

        protected override void Reinit(List<(Transform, Transform)> transforms, List<int> problems)
        {
            if (_boneStaticData.IsCreated) _boneStaticData.Dispose();
            if (_mergeSavedState.IsCreated) _mergeSavedState.Dispose();
            
            _transforms = transforms;

            _boneStaticData = new NativeArray<BoneStaticData>(transforms.Count, Allocator.Persistent);

            _baseBones = new Transform[_transforms.Count];
            _mergeBones = new Transform[_transforms.Count];
            _baseParentBones = new Transform[_transforms.Count];
            _mergeParentBones = new Transform[_transforms.Count];
            _mergeSavedState = new NativeArray<TransformState>(_transforms.Count, Allocator.Persistent);

            for (int i = 0; i < transforms.Count; i++)
            {
                var (baseBone, mergeBone) = transforms[i];
                var mergeParent = mergeBone.parent;
                var baseParent = baseBone.parent;

                if (mergeParent == null || baseParent == null)
                {
                    problems.Add(i);
                    continue;
                }

                if (SmallScale(mergeParent.localScale) || SmallScale(mergeBone.localScale) ||
                    SmallScale(baseBone.localScale))
                {
                    problems.Add(i);
                    continue;
                }

                _baseBones[i] = baseBone;
                _mergeBones[i] = mergeBone;
                _baseParentBones[i] = baseParent;
                _mergeParentBones[i] = mergeParent;

                _mergeSavedState[i] = TransformState.FromTransform(mergeBone);

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
                var mat_l = baseBone.worldToLocalMatrix * mergeBone.localToWorldMatrix;
                // We also find parent -> mergeParent
                var mat_r = mergeParent.worldToLocalMatrix * baseParent.localToWorldMatrix;
                // Now we can multiply:
                // (baseParent -> mergeParent) * (baseBone -> baseParent) * (mergeBone -> baseBone)
                //  = (baseParent -> mergeParent) * (mergeBone -> baseParent)
                //  = (mergeBone -> mergeParent)

                _boneStaticData[i] = new BoneStaticData()
                {
                    _mat_l = mat_r,
                    _mat_r = mat_l
                };
            }
        }

        private bool SmallScale(Vector3 scale)
        {
            var epsilon = 0.000001f;

            return (scale.x < epsilon || scale.y < epsilon || scale.z < epsilon);
        }

        protected override JobHandle Compute(ArmatureLockJobAccessor accessor, int? jobIndex, JobHandle dependency)
        {
            return new ComputePosition()
            {
                _baseState = accessor._in_baseBone,
                _mergeState = accessor._in_targetBone,
                _mergeSavedState = _mergeSavedState,
                _boneStatic = _boneStaticData,
                _fault = accessor._abortFlag,
                _wroteAny = accessor._didAnyWriteFlag,
                _wroteBone = accessor._out_dirty_targetBone,
                jobIndexLimit = jobIndex ?? -1,
                _boneToJobIndex = accessor._boneToJobIndex,
                _outputState = accessor._out_targetBone,
            }.Schedule(accessor._in_baseBone.Length, 32, dependency);
        }

        protected override void DerivedDispose()
        {
            if (_boneStaticData.IsCreated) _boneStaticData.Dispose();
            if (_mergeSavedState.IsCreated) _mergeSavedState.Dispose();
        }

        struct BoneStaticData
        {
            public Matrix4x4 _mat_l, _mat_r;
        }


        [BurstCompile]
        struct ComputePosition : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BoneStaticData> _boneStatic;

            [ReadOnly] public NativeArray<TransformState> _mergeState;
            [ReadOnly] public NativeArray<TransformState> _baseState;

            public NativeArray<TransformState> _mergeSavedState;
            public NativeArray<TransformState> _outputState;
            public NativeArray<int> _wroteBone;

            public int jobIndexLimit;

            [ReadOnly] public NativeArray<int> _boneToJobIndex;

            // job indexed
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public NativeArray<int> _fault, _wroteAny;

            public void Execute(int index)
            {
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
                    _fault[jobIndex] = 1;
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
                    _wroteAny[jobIndex] = 1;
                    _wroteBone[index] = 1;
                    _mergeSavedState[index] = newState;
                    _outputState[index] = newState;
                }
            }
        }
    }
}