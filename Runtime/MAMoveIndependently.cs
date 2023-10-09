using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.armature_lock;
using UnityEditor;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDKBase;
#endif

namespace nadena.dev.modular_avatar.core.ArmatureAwase
{
    [ExecuteInEditMode]
    //[AddComponentMenu("")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/move-independently?lang=auto")]
    class MAMoveIndependently : MonoBehaviour, IEditorOnly
    {
        private float EPSILON = 0.000001f;

        private GameObject[] m_groupedBones;

        public GameObject[] GroupedBones
        {
            get => m_groupedBones.Clone() as GameObject[];
            set
            {
                m_groupedBones = value.Clone() as GameObject[];
                OnValidate();
            }
        }

        private Matrix4x4 _priorFrameState;

        struct ChildState
        {
            internal Vector3 childLocalPos;
            internal Quaternion childLocalRot;
            internal Vector3 childLocalScale;

            // The child world position, recorded when we first initialized (or after unexpected child movement)
            internal Matrix4x4 childWorld;
        }

        private Dictionary<Transform, ChildState> _children = new Dictionary<Transform, ChildState>();
        private HashSet<Transform> _excluded = new HashSet<Transform>();

        void Awake()
        {
            hideFlags = HideFlags.DontSave;
        }

        // We need to reparent the TRS values of the children from our prior frame state to the current frame state.
        // This is done by computing the world affine matrix for the child in the prior frame, then converting to
        // a local affine matrix in the current frame.

        private void OnValidate()
        {
            Debug.Log("=== OnValidate");
            hideFlags = HideFlags.DontSave;
            _excluded = new HashSet<Transform>();
            if (m_groupedBones == null)
            {
                m_groupedBones = Array.Empty<GameObject>();
            }

            foreach (var grouped in m_groupedBones)
            {
                if (grouped != null)
                {
                    _excluded.Add(grouped.transform);
                }
            }

            _priorFrameState = transform.localToWorldMatrix;
            _children.Clear();
            CheckChildren();
        }

        HashSet<Transform> _observed = new HashSet<Transform>();

        private void CheckChildren()
        {
            _observed.Clear();

            CheckChildren(transform);
            foreach (var obj in m_groupedBones)
            {
                CheckChildren(obj.transform);
            }

            // Remove any children that are no longer children
            var toRemove = new List<Transform>();
            foreach (var child in _children)
            {
                if (child.Key == null || !_observed.Contains(child.Key))
                {
                    toRemove.Add(child.Key);
                }
            }

            foreach (var child in toRemove)
            {
                _children.Remove(child);
            }
        }

        private void CheckChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (_excluded.Contains(child)) continue;

                _observed.Add(child);

                var localPosition = child.localPosition;
                var localRotation = child.localRotation;
                var localScale = child.localScale;

                if (!ArmatureLockController.MovedThisFrame && _children.TryGetValue(child, out var state))
                {
                    var deltaPos = localPosition - state.childLocalPos;
                    var deltaRot = Quaternion.Angle(localRotation, state.childLocalRot);
                    var deltaScale = (localScale - state.childLocalScale).sqrMagnitude;

                    if (deltaPos.sqrMagnitude < EPSILON && deltaRot < EPSILON && deltaScale < EPSILON)
                    {
                        Matrix4x4 childNewLocal = parent.worldToLocalMatrix * state.childWorld;

                        var newPosition = childNewLocal.MultiplyPoint(Vector3.zero);
                        var newRotation = childNewLocal.rotation;
                        var newScale = childNewLocal.lossyScale;

                        if ((newPosition - localPosition).sqrMagnitude > EPSILON
                            || Quaternion.Angle(newRotation, localRotation) > EPSILON
                            || (newScale - localScale).sqrMagnitude > EPSILON)
                        {
#if UNITY_EDITOR
                            UnityEditor.Undo.RecordObject(child, UnityEditor.Undo.GetCurrentGroupName());
#endif

                            child.localPosition = newPosition;
                            child.localRotation = newRotation;
                            child.localScale = newScale;

                            state.childLocalPos = child.localPosition;
                            state.childLocalRot = child.localRotation;
                            state.childLocalScale = child.localScale;
                        }

                        _children[child] = state;

                        continue;
                    }
                }

                Matrix4x4 childTRS = Matrix4x4.TRS(localPosition, localRotation, localScale);

                state = new ChildState()
                {
                    childLocalPos = localPosition,
                    childLocalRot = localRotation,
                    childLocalScale = localScale,
                    childWorld = parent.localToWorldMatrix * childTRS,
                };

                _children[child] = state;
            }
        }

        private void OnEnable()
        {
            UpdateLoopController.OnMoveIndependentlyUpdate += OnUpdate;
        }

        private void OnDisable()
        {
            UpdateLoopController.OnMoveIndependentlyUpdate -= OnUpdate;
        }

        void OnUpdate()
        {
            if (this == null)
            {
                UpdateLoopController.OnMoveIndependentlyUpdate -= OnUpdate;
                return;
            }

            var deltaPos = transform.position - _priorFrameState.MultiplyPoint(Vector3.zero);
            var deltaRot = Quaternion.Angle(_priorFrameState.rotation, transform.rotation);
            var deltaScale = (transform.lossyScale - _priorFrameState.lossyScale).sqrMagnitude;

            if (deltaPos.sqrMagnitude < EPSILON && deltaRot < EPSILON && deltaScale < EPSILON) return;

            CheckChildren();
        }
    }
}