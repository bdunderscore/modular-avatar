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
        private ArmatureLockJobAccessor _accessor;

        private TransformAccessArray _baseBones, _targetBones;

        private int _commitFilter;

        private bool _isDisposed = false;
        private bool _isInit = false, _isValid = false;

        private ImmutableList<ArmatureLockJob> _jobs = ImmutableList<ArmatureLockJob>.Empty;
        private JobHandle _lastJob;
        private List<ArmatureLockJob> _requestedJobs = new List<ArmatureLockJob>();
        private long LastCheckedHierarchy = -1;

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
        }

        protected abstract bool WritesBaseBones { get; }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (!_isInit) return;

            _lastJob.Complete();
            DeferDestroy.DeferDestroyObj(_baseBones);
            DeferDestroy.DeferDestroyObj(_targetBones);
            DerivedDispose();
            _accessor.Destroy();
        }

#if UNITY_EDITOR
        protected static void StaticInit()
        {
            EditorApplication.hierarchyChanged += () => { LastHierarchyChange += 1; };
            UpdateLoopController.UpdateCallbacks += Instance.Update;
            ArmatureLockConfig.instance.OnGlobalEnableChange += Instance.Invalidate;

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
        /// Initialize the lock operator with a particular list of transforms.
        /// </summary>
        /// <param name="transforms"></param>
        protected abstract void Reinit(List<(Transform, Transform)> transforms, List<int> problems);

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
            ArmatureLockJob job = null;
            job = new ArmatureLockJob(
                transforms.ToImmutableList(),
                () => RemoveJob(job),
                () => UpdateSingle(job)
            );

            _requestedJobs.Add(job);
            Invalidate();

            return job;
        }

        private void Invalidate()
        {
            _isValid = false;
        }

        private void MaybeRevalidate()
        {
            if (!_isValid)
            {
                // Do an update to make sure all the old jobs are in sync first, before we reset our state.
                if (_isInit) SingleUpdate(null);
                Reset();
            }
        }

        private void Reset()
        {
            if (_isDisposed) return;

            _lastJob.Complete();

            if (_isInit)
            {
                _accessor.Destroy();
                _baseBones.Dispose();
                _targetBones.Dispose();
            }

            _isInit = true;

            // TODO: toposort?
            int[] boneToJobIndex = null;

            List<int> problems = new List<int>();
            do
            {
                var failed = problems.Select(p => _jobs[boneToJobIndex[p]]).Distinct().ToList();
                foreach (var job in failed)
                {
                    job.IsValid = false;
                    _requestedJobs.Remove(job);
                }

                problems.Clear();

                _jobs = _requestedJobs.ToImmutableList();

                _accessor.Destroy();
                if (_baseBones.isCreated) _baseBones.Dispose();
                if (_targetBones.isCreated) _targetBones.Dispose();

                _baseBones = _targetBones = default;

                var bones = _jobs.SelectMany(j => j.Transforms).ToList();
                boneToJobIndex = _jobs.SelectMany((i, j) => Enumerable.Repeat(j, i.Transforms.Count)).ToArray();

                var baseBones = bones.Select(t => t.Item1).ToArray();
                var targetBones = bones.Select(t => t.Item2).ToArray();

                _accessor.Allocate(
                    bones.Count,
                    _jobs.Count
                );

                _baseBones = new TransformAccessArray(baseBones);
                _targetBones = new TransformAccessArray(targetBones);

                Reinit(_jobs.SelectMany(j => j.Transforms).ToList(), problems);
            } while (problems.Count > 0);

            _isValid = true;
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

            MaybeRevalidate();

            SingleUpdate(jobIndex);
        }

        private long CycleStartHierarchyIndex = -1;
        private int _nextCheckIndex = 0;

        private void SingleUpdate(int? jobIndex)
        {
            if (!_isInit || _jobs.Count == 0) return;

            Profiler.BeginSample("InternalUpdate");
            _lastJob.Complete();

            for (int i = 0; i < _jobs.Count; i++)
            {
                _accessor._abortFlag[i] = 0;
                _accessor._didAnyWriteFlag[i] = 0;
            }

            _lastJob = ReadTransforms(jobIndex);
            _lastJob = Compute(_accessor, jobIndex, _lastJob);

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

                    if (job.HierarchyChanged)
                    {
                        job.IsValid = false;
                        Invalidate();
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
            int boneBase = 0;
            bool anyDirty = false;
            for (int job = 0; job < _jobs.Count; job++)
            {
                int curBoneBase = boneBase;
                boneBase += _jobs[job].Transforms.Count;
                if (_accessor._didAnyWriteFlag[job] == 0) continue;

                for (int b = curBoneBase; b < boneBase; b++)
                {
                    if (_accessor._out_dirty_targetBone[b] != 0 || _accessor._out_dirty_baseBone[b] != 0)
                    {
                        anyDirty = true;

                        if (_jobs[job].BoneChanged(b - curBoneBase))
                        {
                            _accessor._abortFlag[job] = 1;
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
                if (_accessor._abortFlag[i] != 0)
                {
                    Invalidate();
                }
                else
                {
                    _jobs[i].MarkLoop();
                }

                _jobs[i].WroteAny = _accessor._didAnyWriteFlag[i] != 0;
            }

            if (!_isValid)
            {
                Reset();
            }

            Profiler.EndSample();
        }

        private void RemoveJob(ArmatureLockJob job)
        {
            if (_requestedJobs.Remove(job)) Invalidate();
        }

        protected abstract void DerivedDispose();

        #region Job logic

        [BurstCompile]
        struct ReadTransformsJob : IJobParallelForTransform
        {
            public NativeArray<TransformState> _bone;
            public NativeArray<TransformState> _bone2;

            [ReadOnly] public NativeArray<int> _boneToJobIndex;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public NativeArray<int> _abortFlag;

            [BurstCompile]
            public void Execute(int index, TransformAccess transform)
            {
#if UNITY_2021_1_OR_NEWER
                if (!transform.isValid)
                {
                    _abortFlag[_boneToJobIndex[index]] = 1;
                    return;
                }
#endif

                _bone[index] = _bone2[index] = new TransformState
                {
                    localPosition = transform.localPosition,
                    localRotation = transform.localRotation,
                    localScale = transform.localScale
                };
            }
        }

        JobHandle ReadTransforms(int? jobIndex)
        {
            var baseRead = new ReadTransformsJob()
            {
                _bone = _accessor._in_baseBone,
                _bone2 = _accessor._out_baseBone,
                _boneToJobIndex = _accessor._boneToJobIndex,
                _abortFlag = _accessor._abortFlag
            }.ScheduleReadOnly(_baseBones, 32);

            var targetRead = new ReadTransformsJob()
            {
                _bone = _accessor._in_targetBone,
                _bone2 = _accessor._out_targetBone,
                _boneToJobIndex = _accessor._boneToJobIndex,
                _abortFlag = _accessor._abortFlag
            }.ScheduleReadOnly(_targetBones, 32, baseRead);

            return JobHandle.CombineDependencies(baseRead, targetRead);
        }

        [BurstCompile]
        struct CommitTransformsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<TransformState> _boneState;
            [ReadOnly] public NativeArray<int> _dirtyBoneFlag;
            [ReadOnly] public NativeArray<int> _boneToJobIndex;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction] [ReadOnly]
            public NativeArray<int> _abortFlag;

            public int jobIndexFilter;

            [BurstCompile]
            public void Execute(int index, TransformAccess transform)
            {
#if UNITY_2021_1_OR_NEWER
                if (!transform.isValid) return;
#endif

                var jobIndex = _boneToJobIndex[index];
                if (jobIndexFilter >= 0 && jobIndex != jobIndexFilter) return;
                if (_abortFlag[jobIndex] != 0) return;
                if (_dirtyBoneFlag[index] == 0) return;

                transform.localPosition = _boneState[index].localPosition;
                transform.localRotation = _boneState[index].localRotation;
                transform.localScale = _boneState[index].localScale;
            }
        }

        JobHandle CommitTransforms(int? jobIndex, JobHandle prior)
        {
            JobHandle job = new CommitTransformsJob()
            {
                _boneState = _accessor._out_targetBone,
                _dirtyBoneFlag = _accessor._out_dirty_targetBone,
                _boneToJobIndex = _accessor._boneToJobIndex,
                _abortFlag = _accessor._abortFlag,
                jobIndexFilter = jobIndex ?? -1
            }.Schedule(_targetBones, prior);

            if (WritesBaseBones)
            {
                var job2 = new CommitTransformsJob()
                {
                    _boneState = _accessor._out_baseBone,
                    _dirtyBoneFlag = _accessor._out_dirty_baseBone,
                    _boneToJobIndex = _accessor._boneToJobIndex,
                    _abortFlag = _accessor._abortFlag,
                    jobIndexFilter = jobIndex ?? -1
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