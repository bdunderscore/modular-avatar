using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using VRC.SDKBase;

namespace nadena.dev.modular_avatar.core.ArmatureAwase
{
    [ExecuteInEditMode]
    //[AddComponentMenu("")]
    [DisallowMultipleComponent]
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

                if (_children.TryGetValue(child, out var state))
                {
                    var deltaPos = localPosition - state.childLocalPos;
                    var deltaRot = Quaternion.Angle(localRotation, state.childLocalRot);
                    var deltaScale = (localScale - state.childLocalScale).sqrMagnitude;

                    if (deltaPos.sqrMagnitude < EPSILON && deltaRot < EPSILON && deltaScale < EPSILON)
                    {
                        Matrix4x4 childNewLocal = parent.worldToLocalMatrix * state.childWorld;
#if UNITY_EDITOR
                        Undo.RecordObject(child, Undo.GetCurrentGroupName());
#endif
                        child.localPosition = childNewLocal.MultiplyPoint(Vector3.zero);
                        child.localRotation = childNewLocal.rotation;
                        child.localScale = childNewLocal.lossyScale;

                        state.childLocalPos = child.localPosition;
                        state.childLocalRot = child.localRotation;
                        state.childLocalScale = child.localScale;

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

        void Update()
        {
            var deltaPos = transform.position - _priorFrameState.MultiplyPoint(Vector3.zero);
            var deltaRot = Quaternion.Angle(_priorFrameState.rotation, transform.rotation);
            var deltaScale = (transform.lossyScale - _priorFrameState.lossyScale).sqrMagnitude;

            if (deltaPos.sqrMagnitude < EPSILON && deltaRot < EPSILON && deltaScale < EPSILON) return;

            CheckChildren();
        }
    }
}