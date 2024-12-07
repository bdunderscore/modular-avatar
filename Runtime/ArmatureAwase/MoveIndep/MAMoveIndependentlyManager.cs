using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal class MaMoveIndependentlyManager
    {
        internal static MaMoveIndependentlyManager Instance { get; } = new();

        private MaMoveIndependentlyManager()
        {
            _nativeMemoryManager = new NativeMemoryManager();

            _vpState = _nativeMemoryManager.CreateArray<TransformState>();
            _tpState = _nativeMemoryManager.CreateArray<TransformState>();
            _targetState = _nativeMemoryManager.CreateArray<TransformState>();
            _mappingStates = _nativeMemoryManager.CreateArray<MappingState>();

            _errorFlags = _nativeMemoryManager.CreateArray<bool>();
            _enabled = _nativeMemoryManager.CreateArray<bool>();
            _sceneRootParent = _nativeMemoryManager.CreateArray<bool>();
            _falseArray = _nativeMemoryManager.CreateArray<bool>();

            _anyError = new NativeArray<bool>(1, Allocator.Persistent);
            _anyDirty = new NativeArray<bool>(1, Allocator.Persistent);

            _nativeMemoryManager.OnSegmentMove += MoveTransforms;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnDomainUnload;
#endif
        }

        private void OnDomainUnload()
        {
            Dispose();
        }

        private void Dispose()
        {
            _lastJob.Complete();

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnDomainUnload;
#endif

            if (_virtualParents.isCreated) DeferDestroy.DeferDestroyObj(_virtualParents);
            if (_trueParents.isCreated) DeferDestroy.DeferDestroyObj(_trueParents);
            if (_targets.isCreated) DeferDestroy.DeferDestroyObj(_targets);

            _nativeMemoryManager.Dispose();

            if (_anyError.IsCreated) _anyError.Dispose();
            if (_anyDirty.IsCreated) _anyDirty.Dispose();
        }

        private const float PosEpsilon = 0.0000001f;
        private const float RotEpsilon = 0.0000001f;
        private const float ScaleEpsilon = 0.0000001f;

        // Our basic strategy is to identify all children of MoveIndependently objects, and to find the first parent
        // that is not a member of the same MoveIndependently group. We then compute the local transform of the child
        // relative to that parent, and keep it constant (unless the true local transform of the child changes).
        // 
        // If an active MAMoveIndep is a child of another MAMoveIndep, we consider it to be ungrouped (even if it's
        // named in the parent).

        private readonly NativeMemoryManager _nativeMemoryManager;

        private Transform[] _virtualParentsT;
        private Transform[] _trueParentsT;
        private Transform[] _targetsT;

        private TransformAccessArray _virtualParents;
        private TransformAccessArray _trueParents;
        private TransformAccessArray _targets;

        private bool _transformAccessDirty;

        private readonly NativeArrayRef<TransformState> _vpState;
        private readonly NativeArrayRef<TransformState> _tpState;
        private readonly NativeArrayRef<TransformState> _targetState;
        private readonly NativeArrayRef<MappingState> _mappingStates;
        private readonly NativeArrayRef<bool> _errorFlags;
        private NativeArray<bool> _anyError, _anyDirty;
        private readonly NativeArrayRef<bool> _enabled;
        private readonly NativeArrayRef<bool> _sceneRootParent, _falseArray;
        private readonly Dictionary<int, State> _slotToState = new();

        private struct MappingState
        {
            // Our last observed local transform, relative to our actual parent transform
            public TransformState TrueLocal;

            // Our last observed local transform, relative to our virtual parent transform
            public Matrix4x4 VirtualLocal;

            // The position of our parent relative to our virtual parent
            public Matrix4x4 TrueLocalToVirtualLocal;
            public bool RequestWriteback, CacheValid;
        }

        private class State
        {
            public MAMoveIndependently MoveIndep;
            public ISegment Segment;
        }

        private readonly Dictionary<MAMoveIndependently, State> _moveIndeps = new();
        private JobHandle _lastJob;

        private bool _isRegistered;
        private int _maxComputeDepth;

        private bool UpdateRegistered
        {
            get => _isRegistered;
            set
            {
                if (value == _isRegistered) return;
                if (value)
                {
                    UpdateLoopController.OnMoveIndependentlyUpdate += Update;
                }
                else
                {
                    UpdateLoopController.OnMoveIndependentlyUpdate -= Update;
                }

                _isRegistered = value;
            }
        }

        private void EnsureTransformCapacity(int targetLength)
        {
            if (_virtualParentsT == null)
            {
                _virtualParentsT = new Transform[targetLength];
                _trueParentsT = new Transform[targetLength];
                _targetsT = new Transform[targetLength];
                return;
            }

            if (targetLength <= _virtualParentsT.Length) return;

            var newCapacity = Mathf.Max(_virtualParentsT.Length * 2, targetLength);
            Array.Resize(ref _virtualParentsT, newCapacity);
            Array.Resize(ref _trueParentsT, newCapacity);
            Array.Resize(ref _targetsT, newCapacity);
        }

        private void MoveTransforms(int oldoffset, int newoffset, int length)
        {
            Array.Copy(_virtualParentsT, oldoffset, _virtualParentsT, newoffset, length);
            Array.Copy(_trueParentsT, oldoffset, _trueParentsT, newoffset, length);
            Array.Copy(_targetsT, oldoffset, _targetsT, newoffset, length);

            _transformAccessDirty = true;
        }

        private void UpdateTransformAccess()
        {
            if (!_transformAccessDirty) return;

            UpdateTransformAccess(ref _virtualParents, _virtualParentsT);
            UpdateTransformAccess(ref _trueParents, _trueParentsT);
            UpdateTransformAccess(ref _targets, _targetsT);

            _transformAccessDirty = false;
        }

        private void UpdateTransformAccess(ref TransformAccessArray arr, Transform[] t)
        {
            if (!arr.isCreated || arr.length != t.Length)
            {
                if (arr.isCreated) arr.Dispose();
                arr = new TransformAccessArray(t);
            }
            else
            {
                arr.SetTransforms(t);
            }
        }

        private void Update()
        {
            _lastJob.Complete();

            UpdateTransformAccess();

            _anyError[0] = false;
            _anyDirty[0] = false;
            
            var readVp = new JReadTransforms
            {
                States = _vpState,
                Enabled = _enabled,
                ErrorFlags = _errorFlags,
                SceneRootParent = _sceneRootParent
            };
            var readTp = new JReadTransforms
            {
                States = _tpState,
                Enabled = _enabled,
                ErrorFlags = _errorFlags,
                SceneRootParent = _falseArray
            };
            var readTarget = new JReadTransforms
            {
                States = _targetState,
                Enabled = _enabled,
                ErrorFlags = _errorFlags,
                SceneRootParent = _falseArray
            };

            var clearVpHandle = new JInitLocalStateAndErrorFlags
            {
                States = _vpState,
                SceneRootParent = _sceneRootParent,
                ErrorFlags = _errorFlags
            }.Schedule(_vpState.Length, 16);

            var readVpHandle = readVp.Schedule(_virtualParents, clearVpHandle);
            var readTpHandle = readTp.Schedule(_trueParents, clearVpHandle);
            var readTargetHandle = readTarget.Schedule(_targets, clearVpHandle);
            var readHandle = JobHandle.CombineDependencies(readVpHandle, readTpHandle, readTargetHandle);

            var compute = new JCompute
            {
                VpState = _vpState,
                TpState = _tpState,
                TargetState = _targetState,
                States = _mappingStates,
                AnyDirty = _anyDirty,
                AnyError = _anyError,
                ErrorFlags = _errorFlags,
                Enabled = _enabled
            };

            var computeHandle = compute.Schedule(_mappingStates.Length, 16, readHandle);
            _lastJob = computeHandle;

            computeHandle.Complete();

            List<Transform> prefabRecord = null;
            if (_anyDirty[0])
            {
                prefabRecord = new List<Transform>();
                for (var i = 0; i < _mappingStates.Length; i++)
                {
                    if (_mappingStates[i].RequestWriteback)
                    {
#if UNITY_EDITOR
                        Undo.RecordObject(_targets[i], "Move Independently");
#endif
                        prefabRecord.Add(_targets[i]);
                    }
                }
            }

            var writeback = new JWriteback
            {
                States = _mappingStates,
                Errors = _errorFlags,
                Enabled = _enabled,
                AnyError = _anyError
            };

            var writebackHandle = writeback.Schedule(_targets, computeHandle);

            _lastJob = writebackHandle;

            writebackHandle.Complete();

            if (prefabRecord != null)
            {
                foreach (var transform in prefabRecord)
                {
#if UNITY_EDITOR
                    PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
#endif
                }
            }

            if (_anyError[0])
            {
                List<MAMoveIndependently> reactivate = new();
                for (var i = 0; i < _mappingStates.Length; i++)
                {
                    if (_errorFlags[i] && _slotToState.TryGetValue(i, out var state))
                    {
                        if (state != null)
                        {
                            Deactivate(state);
                            reactivate.Add(state.MoveIndep);
                        }
                        else
                        {
                            // Hmm - how did we end up with a null here?
                            _slotToState.Remove(i);
                            _enabled[i] = false;
                        }
                    }
                }

                foreach (var moveIndep in reactivate)
                {
                    if (moveIndep != null) Activate(moveIndep);
                }
            }
        }

        internal void Activate(MAMoveIndependently moveIndep)
        {
            if (!_anyDirty.IsCreated) return; // domain reload timing issues

            if (_moveIndeps.TryGetValue(moveIndep, out var state)) Deactivate(state);

            HashSet<Transform> groupedTransforms = new();
            groupedTransforms.Add(moveIndep.transform);

            RegisterGroupedTransforms(moveIndep, groupedTransforms);

            List<MAMoveIndependently> toReregister = new();

            foreach (var t in groupedTransforms)
            {
                // If we have a direct child MAMI, we need it to change its virtual parent, so trigger a reregister
                // on it.
                if (t.TryGetComponent<MAMoveIndependently>(out var mami) && mami != moveIndep)
                {
                    toReregister.Add(mami);
                }
            }

            var ptr = moveIndep.transform.parent;
            while (ptr != null)
            {
                var parentMoveIndep = ptr.GetComponentInParent<MAMoveIndependently>();
                if (parentMoveIndep == null) break;

                RegisterGroupedTransforms(parentMoveIndep, groupedTransforms);

                ptr = parentMoveIndep.transform.parent;
            }

            // Compute leaf transforms
            List<Transform> leafTransforms = new();
            Walk(moveIndep.transform);

            var segment = _nativeMemoryManager.Allocate(leafTransforms.Count);
            EnsureTransformCapacity(segment.Offset + segment.Length);
            _transformAccessDirty = true;

            var virtualParent = moveIndep.transform.parent;
            while (virtualParent != null && groupedTransforms.Contains(virtualParent))
                virtualParent = virtualParent.parent;

            for (var i = 0; i < leafTransforms.Count; i++)
            {
                var j = i + segment.Offset;
                _mappingStates[j] = new MappingState
                {

                    CacheValid = false
                };

                _virtualParentsT[j] = virtualParent;
                _trueParentsT[j] = leafTransforms[i].parent;
                _targetsT[j] = leafTransforms[i];
                _enabled[j] = true;
                _sceneRootParent[j] = virtualParent == null;
                _slotToState[j] = state;
            }

            _moveIndeps[moveIndep] = new State
            {
                MoveIndep = moveIndep,
                Segment = segment
            };

            UpdateRegistered = true;

            foreach (var mami in toReregister)
            {
                if (mami != null) Activate(mami);
            }

            void Walk(Transform t)
            {
                foreach (Transform child in t)
                {
                    if (groupedTransforms.Contains(child))
                    {
                        Walk(child);
                        continue;
                    }

                    leafTransforms.Add(child);
                }
            }
        }

        private void RegisterGroupedTransforms(MAMoveIndependently moveIndep, HashSet<Transform> groupedTransforms)
        {
            var candidates = new HashSet<GameObject>(moveIndep.GroupedBones);
            candidates.Add(moveIndep.gameObject);

            Walk(moveIndep.transform);

            void Walk(Transform t)
            {
                if (!candidates.Contains(t.gameObject)) return;

                groupedTransforms.Add(t);

                foreach (Transform child in t)
                {
                    if (child.TryGetComponent<MAMoveIndependently>(out _)) continue;

                    Walk(child);
                }
            }
        }

        internal void Deactivate(MAMoveIndependently moveIndep)
        {
            if (_moveIndeps.TryGetValue(moveIndep, out var state)) Deactivate(state);
        }

        private void Deactivate(State state)
        {
            if (state?.Segment == null) return;
            if (!_anyDirty.IsCreated) return; // domain reload timing issues

            _moveIndeps.Remove(state.MoveIndep);
            if (_moveIndeps.Count == 0) UpdateRegistered = false;
            
            for (var i = 0; i < state.Segment.Length; i++)
            {
                var j = i + state.Segment.Offset;
                _enabled[j] = false;
                if (_virtualParents.isCreated && _virtualParentsT.Length > j) _virtualParents[j] = null;
                if (_trueParents.isCreated && _trueParents.length > j) _trueParents[j] = null;
                if (_targets.isCreated && _targets.length > j) _targets[j] = null;
                _slotToState.Remove(j);
            }

            _nativeMemoryManager.Free(state.Segment);
            state.Segment = null;
        }

        [BurstCompile]
        private static bool MatDiffers(Matrix4x4 a, Matrix4x4 b)
        {
            var aPos = a.GetColumn(3);
            var bPos = b.GetColumn(3);

            if ((aPos - bPos).sqrMagnitude > PosEpsilon) return true;

            var aRot = a.rotation;
            var bRot = b.rotation;

            if (Quaternion.Angle(aRot, bRot) > RotEpsilon) return true;

            var aScale = a.lossyScale;
            var bScale = b.lossyScale;

            return (aScale - bScale).sqrMagnitude > ScaleEpsilon;
        }

        private struct JClearErrorFlags : IJobParallelFor
        {
            [WriteOnly] public NativeArray<bool> ErrorFlags;

            public void Execute(int index)
            {
                ErrorFlags[index] = false;
            }
        }

        // For some reason checking SceneRootParent in JReadTransforms was ignored...?
        // Maybe IJobParallelForTransform doesn't execute on null transforms.
        private struct JInitLocalStateAndErrorFlags : IJobParallelFor
        {
            [ReadOnly] public NativeArray<bool> SceneRootParent;
            [WriteOnly] public NativeArray<TransformState> States;
            [WriteOnly] public NativeArray<bool> ErrorFlags;

            public void Execute(int index)
            {
                ErrorFlags[index] = true;
                
                if (SceneRootParent[index])
                {
                    // Clear error flags since we're effectively handling the read
                    ErrorFlags[index] = false;
                    
                    States[index] = new TransformState
                    {
                        localToWorldMatrix = Matrix4x4.identity,
                        localRotation = Quaternion.identity,
                        localScale = Vector3.one,
                        localPosition = Vector3.zero
                    };
                }
            }
        }

        private struct JReadTransforms : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<TransformState> States;

            [ReadOnly] public NativeArray<bool> Enabled;
            [ReadOnly] public NativeArray<bool> SceneRootParent;
            
            [NativeDisableContainerSafetyRestriction] [WriteOnly]
            public NativeArray<bool> ErrorFlags;

            [BurstCompile]
            public void Execute(int index, TransformAccess transform)
            {
                if (!Enabled[index]) return;

                if (SceneRootParent[index]) return;

                var failed = !transform.isValid;
                ErrorFlags[index] = failed;
                if (failed)
                {
                    return;
                }

                States[index] = new TransformState
                {
                    localToWorldMatrix = transform.localToWorldMatrix,
                    localRotation = transform.localRotation,
                    localScale = transform.localScale,
                    localPosition = transform.localPosition
                };
            }
        }

        private struct JCompute : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TransformState> VpState, TpState, TargetState;

            [WriteOnly] [NativeDisableContainerSafetyRestriction]
            public NativeArray<bool> AnyDirty;

            [WriteOnly] [NativeDisableContainerSafetyRestriction]
            public NativeArray<bool> AnyError;

            public NativeArray<MappingState> States;

            public NativeArray<bool> ErrorFlags;

            [ReadOnly] public NativeArray<bool> Enabled;

            [BurstCompile]
            public void Execute(int index)
            {
                if (!Enabled[index]) return;

                var state = States[index];
                var vp = VpState[index];
                var tp = TpState[index];
                var target = TargetState[index];

                if (ErrorFlags[index])
                {
                    AnyError[0] = true;
                    return;
                }

                // First, compute the virtual parent transform - we'll need it in any case.
                var trueLocalToVirtualLocal = vp.worldToLocalMatrix * tp.localToWorldMatrix;

                state.RequestWriteback = false;

                if (TransformState.Differs(target, state.TrueLocal) || !state.CacheValid)
                {
                    // Our local position changed, so don't try to make any corrections; just remember the new values.
                    state.CacheValid = true;
                    state.TrueLocal = target;
                    state.TrueLocalToVirtualLocal = trueLocalToVirtualLocal;
                    state.VirtualLocal = trueLocalToVirtualLocal * Matrix4x4.TRS(
                        state.TrueLocal.localPosition,
                        state.TrueLocal.localRotation,
                        state.TrueLocal.localScale
                    );
                }
                else if (MatDiffers(trueLocalToVirtualLocal, state.TrueLocalToVirtualLocal))
                {
                    // Our local position didn't change, but our virtual parent did, so we need to correct.
                    // To do this, we take our _old_ virtual local transform, and use it to transform our old true local
                    // position into virtual local space; we then go from _current_ virtual local space to true local. 
                    var virtualLocalToTrueLocal = trueLocalToVirtualLocal.inverse;
                    var trueLocal = virtualLocalToTrueLocal * state.VirtualLocal;

                    state.TrueLocal = new TransformState
                    {
                        localPosition = trueLocal.GetColumn(3),
                        localRotation = trueLocal.rotation,
                        localScale = trueLocal.lossyScale
                    };

                    state.TrueLocalToVirtualLocal = trueLocalToVirtualLocal;

                    state.RequestWriteback = true;
                    AnyDirty[0] = true;
                }

                States[index] = state;
            }
        }

        private struct JWriteback : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<MappingState> States;

            [ReadOnly] public NativeArray<bool> Errors;

            [ReadOnly] public NativeArray<bool> Enabled;

            [NativeDisableContainerSafetyRestriction] [WriteOnly]
            public NativeArray<bool> AnyError;

            [BurstCompile]
            public void Execute(int index, TransformAccess transform)
            {
                var state = States[index];

                if (!Enabled[index] || Errors[index] || !state.RequestWriteback) return;

                if (!transform.isValid)
                {
                    Errors[index] = true;
                    AnyError[0] = true;
                    return;
                }

                var pos = state.TrueLocal.localPosition;
                var rot = state.TrueLocal.localRotation;
                var scale = state.TrueLocal.localScale;

                transform.localPosition = pos;
                transform.localRotation = rot;
                transform.localScale = scale;
            }
        }
    }
}