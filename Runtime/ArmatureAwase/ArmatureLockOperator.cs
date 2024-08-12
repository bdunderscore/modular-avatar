#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal abstract class ArmatureLockOperator<T> : IDisposable where T : ArmatureLockOperator<T>, new()
    {
        internal static readonly T Instance = new T();

        private static long LastHierarchyChange = 0;

        private TransformAccessArray _baseBones, _baseParentBones, _targetBones, _targetParentBones;

        private int _commitFilter;

        private bool _isDisposed = false;

        private JobHandle _lastJob;
        private List<ArmatureLockJob> _jobs = new List<ArmatureLockJob>();
        private long LastCheckedHierarchy = -1;

        protected readonly NativeMemoryManager _memoryManager = new NativeMemoryManager();

        private bool _transformAccessDirty = true;
        private Transform[] _baseTransforms = Array.Empty<Transform>(), _targetTransforms = Array.Empty<Transform>();

        private Transform[] _baseParentTransforms = Array.Empty<Transform>(),
            _targetParentTransforms = Array.Empty<Transform>();

        protected Transform[] BaseTransforms => _baseTransforms;
        protected Transform[] TargetTransforms => _targetTransforms;

        // Managed by _memoryManager
        private NativeArrayRef<TransformState> _in_baseBone, _in_targetBone, _out_baseBone, _out_targetBone;
        private NativeArrayRef<TransformState> _in_baseParentBone, _in_targetParentBone;

        private NativeArrayRef<bool> _out_dirty_baseBone, _out_dirty_targetBone;
        private NativeArrayRef<int> _boneToJobIndex;

        // Not managed by _memoryManager (since they're not indexed by bone)
        private NativeArray<bool> _abortFlag, _didAnyWriteFlag;

        private ArmatureLockJobAccessor GetAccessor()
        {
            return new ArmatureLockJobAccessor()
            {
                _in_baseBone = _in_baseBone,
                _in_targetBone = _in_targetBone,
                _in_baseParentBone = _in_baseParentBone,
                _in_targetParentBone = _in_targetParentBone,
                _out_baseBone = _out_baseBone,
                _out_targetBone = _out_targetBone,
                _out_dirty_baseBone = _out_dirty_baseBone,
                _out_dirty_targetBone = _out_dirty_targetBone,
                _abortFlag = _abortFlag,
                _didAnyWriteFlag = _didAnyWriteFlag,
                _boneToJobIndex = _boneToJobIndex,
                _in_boneInUse = _memoryManager.InUseMask,
            };
        }
        
        static ArmatureLockOperator()
        {
            Instance = new T();
#if UNITY_EDITOR
            EditorApplication.delayCall += StaticInit;
#endif
        }

        protected ArmatureLockOperator()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += () => DeferDestroy.DestroyImmediate(this);
#endif
            _memoryManager.OnSegmentMove += MoveTransforms;

            _in_baseBone = _memoryManager.CreateArray<TransformState>();
            _in_targetBone = _memoryManager.CreateArray<TransformState>();
            _out_baseBone = _memoryManager.CreateArray<TransformState>();
            _out_targetBone = _memoryManager.CreateArray<TransformState>();
            _in_baseParentBone = _memoryManager.CreateArray<TransformState>();
            _in_targetParentBone = _memoryManager.CreateArray<TransformState>();

            _out_dirty_baseBone = _memoryManager.CreateArray<bool>();
            _out_dirty_targetBone = _memoryManager.CreateArray<bool>();
            _boneToJobIndex = _memoryManager.CreateArray<int>();
        }

        protected abstract bool WritesBaseBones { get; }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _lastJob.Complete();
            if (_baseBones.isCreated) DeferDestroy.DeferDestroyObj(_baseBones);
            if (_targetBones.isCreated) DeferDestroy.DeferDestroyObj(_targetBones);
            if (_baseParentBones.isCreated) DeferDestroy.DeferDestroyObj(_baseParentBones);
            if (_targetParentBones.isCreated) DeferDestroy.DeferDestroyObj(_targetParentBones);
            DerivedDispose();

            _memoryManager.Dispose();
            if (_abortFlag.IsCreated) _abortFlag.Dispose();
            if (_didAnyWriteFlag.IsCreated) _didAnyWriteFlag.Dispose();
        }

#if UNITY_EDITOR
        protected static void StaticInit()
        {
            EditorApplication.hierarchyChanged += () => { LastHierarchyChange += 1; };
            UpdateLoopController.UpdateCallbacks += Instance.Update;
            // TODO: On global enable, reset all jobs to init state?
            //ArmatureLockConfig.instance.OnGlobalEnableChange += Instance.Invalidate;

            EditorApplication.playModeStateChanged += (change) =>
            {
                // If we allow ourselves to simply enter play mode without a final update, any movement applied by
                // automatically leaving animation preview mode won't be applied, leaving any outfits in the wrong pose. 
                if (change == PlayModeStateChange.ExitingEditMode)
                {
                    Instance.Update();
                }
            };
        }
#endif

        /// <summary>
        /// (Re-)initialize state for a single job
        /// </summary>
        /// <param name="transforms"></param>
        protected abstract bool SetupJob(ISegment segment);

        /// <summary>
        /// Computes the new positions and status words for a given range of bones.
        /// </summary>
        /// <param name="accessor"></param>
        /// <param name="startBone"></param>
        /// <param name="endBone"></param>
        /// <returns></returns>
        protected abstract JobHandle Compute(ArmatureLockJobAccessor accessor, int? jobIndex, JobHandle dependency);

        public ArmatureLockJob RegisterLock(IEnumerable<(Transform, Transform)> transforms)
        {
            if (_isDisposed) throw new ObjectDisposedException("ArmatureLockOperator");

            var immutableTransforms = transforms.ToImmutableList();

            var segment = _memoryManager.Allocate(immutableTransforms.Count());
            
            ArmatureLockJob job = null;
            job = new ArmatureLockJob(
                segment,
                immutableTransforms,
                () => RemoveJob(job),
                () => UpdateSingle(job)
            );

            EnsureTransformCapacity(_memoryManager.AllocatedLength);

            for (int i = 0; i < job.Transforms.Count(); i++)
            {
                var (baseBone, mergeBone) = job.Transforms[i];
                _baseTransforms[i + segment.Offset] = baseBone;
                _baseParentTransforms[i + segment.Offset] = baseBone.parent;
                _targetTransforms[i + segment.Offset] = mergeBone;
                _targetParentTransforms[i + segment.Offset] = mergeBone.parent;
            }

            int jobIndex = _jobs.IndexOf(null);
            if (jobIndex >= 0)
            {
                _jobs[jobIndex] = job;
            }
            else
            {
                jobIndex = _jobs.Count();
                _jobs.Add(job);
            } 

            EnsureJobFlagCapacity();

            for (int i = 0; i < segment.Length; i++)
            {
                _boneToJobIndex.Array[segment.Offset + i] = jobIndex;
            }

            _transformAccessDirty = true;

            bool ok = false;
            try
            {
                ok = SetupJob(segment);
            }
            finally
            {
                if (!ok)
                {
                    // Initial setup failed; roll things back
                    job.IsValid = false;
                    job.Dispose();
                }
            }

            return job;
        }

        private void RemoveJob(ArmatureLockJob job)
        {
            int index = _jobs.IndexOf(job);

            if (index < 0) return;

            _jobs[index] = null;

            job.Segment.Dispose();
        }

        private void EnsureJobFlagCapacity()
        {
            if (_abortFlag.IsCreated && _abortFlag.Length >= _jobs.Count) return;

            var priorLength = _abortFlag.Length;
            
            if (_abortFlag.IsCreated) _abortFlag.Dispose();
            if (_didAnyWriteFlag.IsCreated) _didAnyWriteFlag.Dispose();

            int targetSize = Math.Max(Math.Max(16, _jobs.Count), (int)(priorLength * 1.5f));
            _abortFlag = new NativeArray<bool>(targetSize, Allocator.Persistent);
            _didAnyWriteFlag = new NativeArray<bool>(targetSize, Allocator.Persistent);
        }

        private void EnsureTransformCapacity(int targetLength)
        {
            if (targetLength == _baseTransforms.Length) return;

            Array.Resize(ref _baseTransforms, targetLength);
            Array.Resize(ref _baseParentTransforms, targetLength);
            Array.Resize(ref _targetTransforms, targetLength);
            Array.Resize(ref _targetParentTransforms, targetLength);
        }

        private void MoveTransforms(int oldoffset, int newoffset, int length)
        {
            Array.Copy(_baseTransforms, oldoffset, _baseTransforms, newoffset, length);
            Array.Copy(_baseParentTransforms, oldoffset, _baseParentTransforms, newoffset, length);
            Array.Copy(_targetTransforms, oldoffset, _targetTransforms, newoffset, length);
            Array.Copy(_targetParentTransforms, oldoffset, _targetParentTransforms, newoffset, length);
            _transformAccessDirty = true;
        }
        
        public void Update()
        {
            InternalUpdate();
        }

        private void UpdateSingle(ArmatureLockJob job)
        {
            var index = _jobs.IndexOf(job);
            if (index < 0) return;

            InternalUpdate(index);
        }

        private void InternalUpdate(int? jobIndex = null)
        {
            if (_isDisposed) return;

            SingleUpdate(jobIndex);
        }

        private long CycleStartHierarchyIndex = -1;
        private int _nextCheckIndex = 0;

        private void SingleUpdate(int? jobIndex)
        {
            if (_jobs.Count == 0) return;

            if (_isDisposed) return;

            Profiler.BeginSample("InternalUpdate");
            _lastJob.Complete();

            EnsureJobFlagCapacity();

            if (_transformAccessDirty)
            {
                Profiler.BeginSample("RecreateTransformAccess");

                if (_baseBones.isCreated && _baseBones.length == _baseTransforms.Length)
                {
                    _baseBones.SetTransforms(_baseTransforms);
                    _baseParentBones.SetTransforms(_baseParentTransforms);
                    _targetBones.SetTransforms(_targetTransforms);
                    _targetParentBones.SetTransforms(_targetParentTransforms);
                }
                else
                {
                    if (_baseBones.isCreated) _baseBones.Dispose();
                    if (_targetBones.isCreated) _targetBones.Dispose();
                    if (_baseParentBones.isCreated) _baseParentBones.Dispose();
                    if (_targetParentBones.isCreated) _targetParentBones.Dispose();

                    _baseBones = new TransformAccessArray(_baseTransforms);
                    _baseParentBones = new TransformAccessArray(_baseParentTransforms);
                    _targetBones = new TransformAccessArray(_targetTransforms);
                    _targetParentBones = new TransformAccessArray(_targetParentTransforms);
                }

                _transformAccessDirty = false;

                Profiler.EndSample();
            }

            var accessor = GetAccessor();
            
            for (int i = 0; i < _jobs.Count; i++)
            {
                accessor._abortFlag[i] = (_jobs[i] == null) || !_jobs[i].IsValid;
                accessor._didAnyWriteFlag[i] = false;
            }

            _lastJob = ReadTransforms(jobIndex);
            _lastJob = Compute(accessor, jobIndex, _lastJob);

            if (LastCheckedHierarchy != LastHierarchyChange)
            {
                Profiler.BeginSample("Recheck");

                int startCheckIndex = _nextCheckIndex;
                do
                {
                    if (_nextCheckIndex == 0)
                    {
                        CycleStartHierarchyIndex = LastHierarchyChange;
                    }

                    var job = _jobs[_nextCheckIndex % _jobs.Count];
                    _nextCheckIndex = (1 + _nextCheckIndex) % _jobs.Count;

                    if (job != null && job.HierarchyChanged)
                    {
                        job.IsValid = false;
                    }
                } while (_nextCheckIndex != startCheckIndex && !_lastJob.IsCompleted);

                if (_nextCheckIndex == 0)
                {
                    LastCheckedHierarchy = CycleStartHierarchyIndex;
                }

                Profiler.EndSample();
            }

            // Before committing, do a spot check of any bones that moved, to see if their parents changed.
            // This is needed because the hierarchyChanged event fires after Update ...

            _lastJob.Complete();
            Profiler.BeginSample("Revalidate dirty bones");
            bool anyDirty = false;
            for (int job = 0; job < _jobs.Count; job++)
            {
                if (accessor._abortFlag[job]) continue;
                if (!accessor._didAnyWriteFlag[job]) continue;

                var curBoneBase = _jobs[job].Segment.Offset;
                var boneEnd = curBoneBase + _jobs[job].Segment.Length;

                for (var b = curBoneBase; b < boneEnd; b++)
                {
                    if (accessor._out_dirty_targetBone[b] || accessor._out_dirty_baseBone[b])
                    {
                        anyDirty = true;

                        if (_jobs[job].BoneChanged(b - curBoneBase))
                        {
                            accessor._abortFlag[job] = true;
                            _jobs[job].IsValid = false;
                            break;
                        }
                    }
                }
            }

            Profiler.EndSample();

            if (anyDirty)
            {
                _lastJob = CommitTransforms(jobIndex, _lastJob);
                _lastJob.Complete();
            }

            for (int i = 0; i < _jobs.Count; i++)
            {
                if (_jobs[i] == null) continue;

                if (accessor._abortFlag[i])
                {
                    _jobs[i].IsValid = false;
                }
                else
                {
                    _jobs[i].MarkLoop();
                    _jobs[i].WroteAny = accessor._didAnyWriteFlag[i];
                }
            }

            Profiler.EndSample();
        }

        protected virtual void DerivedDispose()
        {
            // default no-op
        }

        #region Job logic

        [BurstCompile]
        struct CopyTransformState : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TransformState> _in;
            [WriteOnly] public NativeArray<TransformState> _out;

            public void Execute(int index)
            {
                _out[index] = _in[index];
            }
        }
        
        [BurstCompile]
        struct ReadTransformsJob : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<TransformState> _bone;

            [ReadOnly] public NativeArray<int> _boneToJobIndex;
            [ReadOnly] public NativeArray<bool> _boneInUse;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] [WriteOnly]
            public NativeArray<bool> _abortFlag;

            [BurstCompile]
            public void Execute(int index, TransformAccess transform)
            {
                if (!_boneInUse[index]) return;

#if UNITY_2021_1_OR_NEWER
                if (!transform.isValid && _boneInUse[index])
                {
                    _abortFlag[_boneToJobIndex[index]] = true;
                    return;
                }
#endif

                _bone[index] = new TransformState
                {
                    localPosition = transform.localPosition,
                    localRotation = transform.localRotation,
                    localScale = transform.localScale,
                    localToWorldMatrix = transform.localToWorldMatrix,
                };
            }
        }

        JobHandle ReadTransforms(int? jobIndex)
        {
            var accessor = GetAccessor();
            
            var baseRead = new ReadTransformsJob()
            {
                _bone = accessor._in_baseBone,
                _boneToJobIndex = accessor._boneToJobIndex,
                _abortFlag = accessor._abortFlag,
                _boneInUse = accessor._in_boneInUse,
            }.ScheduleReadOnly(_baseBones, 32);

            baseRead = new CopyTransformState()
            {
                _in = accessor._in_baseBone,
                _out = accessor._out_baseBone
            }.Schedule(accessor._in_baseBone.Length, 32, baseRead);

            var targetRead = new ReadTransformsJob()
            {
                _bone = accessor._in_targetBone,
                _boneToJobIndex = accessor._boneToJobIndex,
                _abortFlag = accessor._abortFlag,
                _boneInUse = accessor._in_boneInUse,
            }.ScheduleReadOnly(_targetBones, 32);

            targetRead = new CopyTransformState()
            {
                _in = accessor._in_targetBone,
                _out = accessor._out_targetBone
            }.Schedule(accessor._in_targetBone.Length, 32, targetRead);

            var baseParentRead = new ReadTransformsJob()
            {
                _bone = accessor._in_baseParentBone,
                _boneToJobIndex = accessor._boneToJobIndex,
                _abortFlag = accessor._abortFlag,
                _boneInUse = accessor._in_boneInUse,
            }.ScheduleReadOnly(_baseParentBones, 32);

            var targetParentRead = new ReadTransformsJob()
            {
                _bone = accessor._in_targetParentBone,
                _boneToJobIndex = accessor._boneToJobIndex,
                _abortFlag = accessor._abortFlag,
                _boneInUse = accessor._in_boneInUse,
            }.ScheduleReadOnly(_targetParentBones, 32);

            return JobHandle.CombineDependencies(
                JobHandle.CombineDependencies(baseRead, targetRead),
                JobHandle.CombineDependencies(baseParentRead, targetParentRead)
            );
        }

        [BurstCompile]
        struct CommitTransformsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<TransformState> _boneState;
            [ReadOnly] public NativeArray<bool> _dirtyBoneFlag;
            [ReadOnly] public NativeArray<int> _boneToJobIndex;
            [ReadOnly] public NativeArray<bool> _boneInUse;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] [ReadOnly]
            public NativeArray<bool> _abortFlag;

            public int jobIndexFilter;

            [BurstCompile]
            public void Execute(int index, TransformAccess transform)
            {
#if UNITY_2021_1_OR_NEWER
                if (!transform.isValid) return;
#endif
                if (!_boneInUse[index]) return;

                var jobIndex = _boneToJobIndex[index];
                if (jobIndexFilter >= 0 && jobIndex != jobIndexFilter) return;
                if (_abortFlag[jobIndex]) return;
                if (!_dirtyBoneFlag[index]) return;

                transform.localPosition = _boneState[index].localPosition;
                transform.localRotation = _boneState[index].localRotation;
                transform.localScale = _boneState[index].localScale;
            }
        }

        JobHandle CommitTransforms(int? jobIndex, JobHandle prior)
        {
            var accessor = GetAccessor();
            
            JobHandle job = new CommitTransformsJob()
            {
                _boneState = accessor._out_targetBone,
                _dirtyBoneFlag = accessor._out_dirty_targetBone,
                _boneToJobIndex = accessor._boneToJobIndex,
                _abortFlag = accessor._abortFlag,
                jobIndexFilter = jobIndex ?? -1,
                _boneInUse = accessor._in_boneInUse,
            }.Schedule(_targetBones, prior);

            if (WritesBaseBones)
            {
                var job2 = new CommitTransformsJob()
                {
                    _boneState = accessor._out_baseBone,
                    _dirtyBoneFlag = accessor._out_dirty_baseBone,
                    _boneToJobIndex = accessor._boneToJobIndex,
                    _abortFlag = accessor._abortFlag,
                    jobIndexFilter = jobIndex ?? -1,
                    _boneInUse = accessor._in_boneInUse,
                }.Schedule(_baseBones, prior);

                return JobHandle.CombineDependencies(job, job2);
            }
            else
            {
                return job;
            }
        }

        #endregion
    }
}