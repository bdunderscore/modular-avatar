using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.JacksonDunstan.NativeCollections;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class OnewayArmatureLock : IDisposable, IArmatureLock
    {
        struct BoneStaticData
        {
            public Matrix4x4 _mat_l, _mat_r;
        }

        private NativeArray<BoneStaticData> _boneStaticData;
        private NativeArray<TransformState> _mergeSavedState;
        private NativeArray<TransformState> _baseState, _mergeState;

        private NativeIntPtr _fault, _wroteAny;

        private readonly Transform[] _baseBones, _mergeBones, _baseParentBones, _mergeParentBones;
        private TransformAccessArray _baseBonesAccessor, _mergeBonesAccessor;

        private bool _disposed;
        private JobHandle LastOp, LastPrepare;

        [BurstCompile]
        struct WriteBone : IJobParallelForTransform
        {
            [ReadOnly] public NativeIntPtr _fault, _shouldWrite;

            [ReadOnly] public NativeArray<TransformState> _values;

            public void Execute(int index, TransformAccess transform)
            {
                if (_fault.Value == 0 && _shouldWrite.Value != 0)
                {
                    var val = _values[index];
                    transform.localPosition = val.localPosition;
                    transform.localRotation = val.localRotation;
                    transform.localScale = val.localScale;
                }
            }
        }

        [BurstCompile]
        struct ComputePosition : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BoneStaticData> _boneStatic;

            [ReadOnly] public NativeArray<TransformState> _mergeState;
            [ReadOnly] public NativeArray<TransformState> _baseState;

            public NativeArray<TransformState> _mergeSavedState;

            public NativeIntPtr.Parallel _fault, _wroteAny;

            public void Execute(int index)
            {
                var boneStatic = _boneStatic[index];
                var mergeState = _mergeState[index];
                var baseState = _baseState[index];
                var mergeSaved = _mergeSavedState[index];

                var basePos = baseState.localPosition;
                var baseRot = baseState.localRotation;
                var baseScale = baseState.localScale;

                if (TransformState.Differs(mergeSaved, mergeState))
                {
                    TransformState.Differs(mergeSaved, mergeState);
                    _fault.Increment();
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
                    _wroteAny.SetOne();
                    _mergeSavedState[index] = newState;
                }
            }
        }

        public OnewayArmatureLock(IReadOnlyList<(Transform, Transform)> mergeToBase)
        {
            _boneStaticData = new NativeArray<BoneStaticData>(mergeToBase.Count, Allocator.Persistent);
            _mergeSavedState = new NativeArray<TransformState>(mergeToBase.Count, Allocator.Persistent);
            _baseState = new NativeArray<TransformState>(mergeToBase.Count, Allocator.Persistent);
            _mergeState = new NativeArray<TransformState>(mergeToBase.Count, Allocator.Persistent);

            _fault = new NativeIntPtr(Allocator.Persistent);
            _wroteAny = new NativeIntPtr(Allocator.Persistent);

            _baseBones = new Transform[mergeToBase.Count];
            _mergeBones = new Transform[mergeToBase.Count];
            _baseParentBones = new Transform[mergeToBase.Count];
            _mergeParentBones = new Transform[mergeToBase.Count];

            try
            {
                for (int i = 0; i < mergeToBase.Count; i++)
                {
                    var (mergeBone, baseBone) = mergeToBase[i];
                    var mergeParent = mergeBone.parent;
                    var baseParent = baseBone.parent;

                    if (mergeParent == null || baseParent == null)
                    {
                        throw new Exception("Can't handle root objects");
                    }

                    if (SmallScale(mergeParent.localScale) || SmallScale(mergeBone.localScale) ||
                        SmallScale(baseBone.localScale))
                    {
                        throw new Exception("Can't handle near-zero scale bones");
                    }

                    _baseBones[i] = baseBone;
                    _mergeBones[i] = mergeBone;
                    _baseParentBones[i] = baseParent;
                    _mergeParentBones[i] = mergeParent;

                    _baseState[i] = TransformState.FromTransform(baseBone);
                    _mergeSavedState[i] = _mergeState[i] = TransformState.FromTransform(mergeBone);

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
            catch (Exception e)
            {
                _boneStaticData.Dispose();
                _mergeSavedState.Dispose();
                _baseState.Dispose();
                _mergeState.Dispose();
                _fault.Dispose();
                _wroteAny.Dispose();

                throw e;
            }

            _baseBonesAccessor = new TransformAccessArray(_baseBones);
            _mergeBonesAccessor = new TransformAccessArray(_mergeBones);

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
#endif
        }

        private bool SmallScale(Vector3 scale)
        {
            var epsilon = 0.000001f;

            return (scale.x < epsilon || scale.y < epsilon || scale.z < epsilon);
        }

        public void Prepare()
        {
            if (_disposed) return;
            
            LastOp.Complete();

            
            _fault.Value = 0;
            _wroteAny.Value = 0;

            var jobReadBase = new ReadBone
            {
                _state = _baseState
            }.Schedule(_baseBonesAccessor);
            var jobReadMerged = new ReadBone
            {
                _state = _mergeState
            }.Schedule(_mergeBonesAccessor);
            var readAll = JobHandle.CombineDependencies(jobReadBase, jobReadMerged);
            LastOp = LastPrepare = new ComputePosition
            {
                _boneStatic = _boneStaticData,
                _mergeState = _mergeState,
                _baseState = _baseState,
                _mergeSavedState = _mergeSavedState,
                _fault = _fault.GetParallel(),
                _wroteAny = _wroteAny.GetParallel(),
            }.Schedule(_baseBones.Length, 32, readAll);
        }

        private bool CheckConsistency()
        {
            if (_disposed) return false;

            // Validate parents while that job is running
            for (int i = 0; i < _baseBones.Length; i++)
            {
                if (_baseBones[i] == null || _mergeBones[i] == null || _baseParentBones[i] == null ||
                    _mergeParentBones[i] == null)
                {
                    return false;
                }

                if (_baseBones[i].parent != _baseParentBones[i] || _mergeBones[i].parent != _mergeParentBones[i])
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsStable()
        {
            Prepare();
            if (!CheckConsistency()) return false;

            LastPrepare.Complete();

            return _fault.Value == 0 && _wroteAny.Value == 0;
        }

        /// <summary>
        /// Executes the armature lock job.
        /// </summary>
        /// <returns>True if successful, false if cached data was invalidated and needs recreating</returns>
        public LockResult Execute()
        {
            if (!CheckConsistency()) return LockResult.Failed;
            
            var commit = new WriteBone()
            {
                _fault = _fault,
                _values = _mergeSavedState,
                _shouldWrite = _wroteAny
            }.Schedule(_mergeBonesAccessor, LastPrepare);

            commit.Complete();

            if (_fault.Value != 0)
            {
                return LockResult.Failed;
            }
            else if (_wroteAny.Value == 0)
            {
                return LockResult.NoOp;
            }
            else
            {
                return LockResult.Success;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            LastOp.Complete();
            _boneStaticData.Dispose();
            _mergeSavedState.Dispose();
            _baseState.Dispose();
            _mergeState.Dispose();
            _fault.Dispose();
            _wroteAny.Dispose();
            _baseBonesAccessor.Dispose();
            _mergeBonesAccessor.Dispose();
            _disposed = true;

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
#endif
        }
    }
}