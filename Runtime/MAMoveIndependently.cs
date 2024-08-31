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
    [AddComponentMenu("Modular Avatar/MA Move Independently")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/move-independently?lang=auto")]
    class MAMoveIndependently : MonoBehaviour, IEditorOnly
    {
        private float EPSILON = 0.0000001f;

        [SerializeField]
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

        struct ChildState
        {
            internal Vector3 childLocalPos;
            internal Quaternion childLocalRot;
            internal Vector3 childLocalScale;

            // The child world position, recorded when we first initialized (or after unexpected child movement)
            internal Matrix4x4 childToRoot;
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

            _priorFramePos = transform.localPosition;
            _priorFrameRot = transform.localRotation;
            _priorFrameScale = transform.localScale;

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

        private Matrix4x4 ParentTransformMatrix(Transform parent)
        {
            Matrix4x4 transform = Matrix4x4.TRS(
                parent.localPosition,
                parent.localRotation,
                parent.localScale
            );

            if (_excluded.Contains(parent))
            {
                transform = ParentTransformMatrix(parent.parent) * transform;
            }

            return transform;
        }

        private void CheckChildren(Transform parent)
        {
            Matrix4x4 parentToRoot = ParentTransformMatrix(parent);
            Matrix4x4 rootToParent = parentToRoot.inverse;

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

                    if (deltaPos.magnitude > EPSILON || deltaRot > EPSILON || deltaScale > EPSILON)
                    {
                        // The child object was moved in between parent updates; reconstruct its childToRoot to correct
                        // for this.
                        var oldChildTRS = Matrix4x4.TRS(
                            state.childLocalPos,
                            state.childLocalRot,
                            state.childLocalScale
                        );

                        var newChildTRS = Matrix4x4.TRS(
                            localPosition,
                            localRotation,
                            localScale
                        );

                        state.childToRoot = state.childToRoot * oldChildTRS.inverse * newChildTRS;
                    }

                    Matrix4x4 childNewLocal = rootToParent * state.childToRoot;

                    var newPosition = childNewLocal.MultiplyPoint(Vector3.zero);
                    var newRotation = childNewLocal.rotation;
                    var newScale = childNewLocal.lossyScale;
#if UNITY_EDITOR
                    Undo.RecordObject(child, Undo.GetCurrentGroupName());
#endif

                    child.localPosition = newPosition;
                    child.localRotation = newRotation;
                    child.localScale = newScale;

                    state.childLocalPos = child.localPosition;
                    state.childLocalRot = child.localRotation;
                    state.childLocalScale = child.localScale;

                    _children[child] = state;

                    continue;
                }

                Matrix4x4 childTRS = Matrix4x4.TRS(localPosition, localRotation, localScale);

                state = new ChildState()
                {
                    childLocalPos = localPosition,
                    childLocalRot = localRotation,
                    childLocalScale = localScale,
                    childToRoot = parentToRoot * childTRS,
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

        private Vector3 _priorFramePos, _priorFrameScale;
        private Quaternion _priorFrameRot;

        void OnUpdate()
        {
            if (this == null)
            {
                UpdateLoopController.OnMoveIndependentlyUpdate -= OnUpdate;
                return;
            }

            var pos = transform.localPosition;
            var rot = transform.localRotation;
            var scale = transform.localScale;

            var deltaPos = transform.parent.localToWorldMatrix.MultiplyVector(pos - _priorFramePos);
            var deltaRot = Quaternion.Angle(rot, _priorFrameRot);

            var deltaScaleX = Mathf.Abs((scale - _priorFrameScale).x) / _priorFrameScale.x;
            var deltaScaleY = Mathf.Abs((scale - _priorFrameScale).y) / _priorFrameScale.y;
            var deltaScaleZ = Mathf.Abs((scale - _priorFrameScale).z) / _priorFrameScale.z;

            if (float.IsNaN(deltaScaleX) || float.IsInfinity(deltaScaleX)) deltaScaleX = 1;
            if (float.IsNaN(deltaScaleY) || float.IsInfinity(deltaScaleY)) deltaScaleY = 1;
            if (float.IsNaN(deltaScaleZ) || float.IsInfinity(deltaScaleZ)) deltaScaleZ = 1;

            float maxDeltaScale = Mathf.Max(deltaScaleX, Mathf.Max(deltaScaleY, deltaScaleZ));

            if (deltaPos.magnitude > EPSILON || deltaRot > EPSILON || maxDeltaScale > 0.001)
            {
                CheckChildren();

                _priorFramePos = pos;
                _priorFrameRot = rot;
                _priorFrameScale = scale;
            }
        }
    }
}