using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.JacksonDunstan.NativeCollections;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class BidirectionalArmatureLock : IDisposable, IArmatureLock
    {
        private bool _disposed;
        private TransformAccessArray _baseBoneAccess, _mergeBoneAccess;
        private readonly Transform[] _baseBones, _mergeBones, _baseParentBones, _mergeParentBones;

        private NativeArray<TransformState> BaseBones, MergeBones, SavedMerge;
        private NativeArray<bool> ShouldWriteBase, ShouldWriteMerge;
        private NativeIntPtr WroteAny;

        private JobHandle LastOp;

        public BidirectionalArmatureLock(IReadOnlyList<(Transform, Transform)> bones)
        {
            _baseBones = new Transform[bones.Count];
            _mergeBones = new Transform[bones.Count];
            _baseParentBones = new Transform[bones.Count];
            _mergeParentBones = new Transform[bones.Count];

            BaseBones = new NativeArray<TransformState>(_baseBones.Length, Allocator.Persistent);
            MergeBones = new NativeArray<TransformState>(_baseBones.Length, Allocator.Persistent);
            SavedMerge = new NativeArray<TransformState>(_baseBones.Length, Allocator.Persistent);

            for (int i = 0; i < _baseBones.Length; i++)
            {
                var (mergeBone, baseBone) = bones[i];
                _baseBones[i] = baseBone;
                _mergeBones[i] = mergeBone;
                _baseParentBones[i] = baseBone.parent;
                _mergeParentBones[i] = mergeBone.parent;

                var mergeState = TransformState.FromTransform(mergeBone);
                SavedMerge[i] = mergeState;
                MergeBones[i] = mergeState;
                BaseBones[i] = TransformState.FromTransform(baseBone);
            }

            _baseBoneAccess = new TransformAccessArray(_baseBones);
            _mergeBoneAccess = new TransformAccessArray(_mergeBones);

            ShouldWriteBase = new NativeArray<bool>(_baseBones.Length, Allocator.Persistent);
            ShouldWriteMerge = new NativeArray<bool>(_baseBones.Length, Allocator.Persistent);
            WroteAny = new NativeIntPtr(Allocator.Persistent);
        }

        [BurstCompile]
        struct Compute : IJobParallelForTransform
        {
            public NativeArray<TransformState> BaseBones, SavedMerge;

            [WriteOnly] public NativeArray<TransformState> MergeBones;

            [WriteOnly] public NativeArray<bool> ShouldWriteBase, ShouldWriteMerge;

            [WriteOnly] public NativeIntPtr.Parallel WroteAny;

            public void Execute(int index, TransformAccess mergeTransform)
            {
                var baseBone = BaseBones[index];
                var mergeBone = new TransformState()
                {
                    localPosition = mergeTransform.localPosition,
                    localRotation = mergeTransform.localRotation,
                    localScale = mergeTransform.localScale,
                };
                MergeBones[index] = mergeBone;

                var saved = SavedMerge[index];

                if (TransformState.Differs(saved, mergeBone))
                {
                    ShouldWriteBase[index] = true;
                    ShouldWriteMerge[index] = false;

                    var mergeToBase = mergeBone;
                    BaseBones[index] = mergeToBase;
                    SavedMerge[index] = mergeBone;
                    WroteAny.SetOne();
                }
                else if (TransformState.Differs(saved, baseBone))
                {
                    ShouldWriteMerge[index] = true;
                    ShouldWriteBase[index] = false;

                    MergeBones[index] = baseBone;
                    SavedMerge[index] = baseBone;
                    WroteAny.SetOne();
                }
                else
                {
                    ShouldWriteBase[index] = false;
                    ShouldWriteMerge[index] = false;
                }
            }
        }

        [BurstCompile]
        struct Commit : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<TransformState> BoneState;
            [ReadOnly] public NativeArray<bool> ShouldWrite;

            public void Execute(int index, TransformAccess transform)
            {
                if (ShouldWrite[index])
                {
                    var boneState = BoneState[index];

                    transform.localPosition = boneState.localPosition;
                    transform.localRotation = boneState.localRotation;
                    transform.localScale = boneState.localScale;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            LastOp.Complete();

            _baseBoneAccess.Dispose();
            _mergeBoneAccess.Dispose();
            BaseBones.Dispose();
            MergeBones.Dispose();
            SavedMerge.Dispose();
            ShouldWriteBase.Dispose();
            ShouldWriteMerge.Dispose();
            WroteAny.Dispose();

            _disposed = true;
        }

        private bool DoCompute(out JobHandle handle)
        {
            handle = default;

            if (_disposed) return false;

            WroteAny.Value = 0;

            LastOp.Complete();

            var readBase = new ReadBone()
            {
                _state = BaseBones,
            }.Schedule(_baseBoneAccess);
            LastOp = handle = new Compute()
            {
                BaseBones = BaseBones,
                MergeBones = MergeBones,
                SavedMerge = SavedMerge,
                ShouldWriteBase = ShouldWriteBase,
                ShouldWriteMerge = ShouldWriteMerge,
                WroteAny = WroteAny.GetParallel(),
            }.Schedule(_mergeBoneAccess, readBase);

            // Check parents haven't changed
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
            if (!DoCompute(out var compute)) return false;

            compute.Complete();

            return WroteAny.Value == 0;
        }

        public LockResult Execute()
        {
            if (!DoCompute(out var compute)) return LockResult.Failed;

            var commitBase = new Commit()
            {
                BoneState = BaseBones,
                ShouldWrite = ShouldWriteBase,
            }.Schedule(_baseBoneAccess, compute);
            var commitMerge = new Commit()
            {
                BoneState = MergeBones,
                ShouldWrite = ShouldWriteMerge,
            }.Schedule(_mergeBoneAccess, compute);

            commitBase.Complete();
            commitMerge.Complete();

            if (WroteAny.Value == 0)
            {
                return LockResult.NoOp;
            }
            else
            {
                return LockResult.Success;
            }
        }
    }
}