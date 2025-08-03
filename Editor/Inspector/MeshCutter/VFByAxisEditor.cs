﻿#nullable enable

using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(VertexFilterByAxisComponent))]
    public class VFByAxisEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshCutter/";
        private const string UxmlPath = Root + "VFByAxisEditor.uxml";
        private const string UssPath = Root + "MeshCutterStyles.uss";

        private static VFByAxisEditor? __activeEditing;

        private static Vector3 _lastVector;

        private Transform? _lastReferenceTransform;
        private Quaternion _gizmoQuaternion = Quaternion.identity;

        private Quaternion? GizmoQuaternion
        {
            get
            {
                var refTransform = GetReferenceTransform();

                if (_lastReferenceTransform == refTransform && _lastVector == _axis?.vector3Value)
                {
                    return _gizmoQuaternion;
                }

                if (_axis == null || refTransform == null) return null;

                _lastReferenceTransform = refTransform;
                _lastVector = _axis.vector3Value;
                _gizmoQuaternion =
                    Quaternion.LookRotation(refTransform.TransformDirection(_axis.vector3Value), Vector3.up);

                return _gizmoQuaternion;
            }
            set
            {
                var refTransform = GetReferenceTransform();
                if (refTransform == null || refTransform != _lastReferenceTransform || value == null) return;

                _gizmoQuaternion = value.Value;
                _lastVector = refTransform.InverseTransformDirection(_gizmoQuaternion * Vector3.forward);
                _axis!.vector3Value = _lastVector;
            }
        }

        private static VFByAxisEditor? _activeEditing
        {
            get => __activeEditing;
            set
            {
                if (value == __activeEditing) return;
                if (__activeEditing != null)
                {
                    __activeEditing.SetEditingState(false);
                }

                __activeEditing = value;
            }
        }

        private SerializedProperty? _axis;
        private SerializedProperty? _center;

        private VisualElement? _root;

        private void OnEnable()
        {
            _axis = serializedObject.FindProperty(nameof(VertexFilterByAxisComponent.m_axis));
            _center = serializedObject.FindProperty(nameof(VertexFilterByAxisComponent.m_center));
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.Bind(serializedObject);

            _root = uxml;

            var btnEdit = uxml.Q<Button>("f-axis-edit");
            btnEdit.clickable.clicked += () => { SetEditingState(_activeEditing != this); };

            var btnFlip = uxml.Q<Button>("f-axis-flip");
            btnFlip.clickable.clicked += () =>
            {
                serializedObject.Update();
                if (_axis != null)
                {
                    _axis.vector3Value = -_axis.vector3Value;
                }

                serializedObject.ApplyModifiedProperties();
            };

            return uxml;
        }

        protected override void OnInnerInspectorGUI()
        {
            DrawDefaultInspector();
        }

        private void SetEditingState(bool editing)
        {
            if (editing)
            {
                _activeEditing = this;
                _root?.AddToClassList("st-active-editing");
                _lastReferenceTransform = null;
            }
            else
            {
                // Avoid recursion by resetting the underlying static variable
                if (_activeEditing == this) __activeEditing = null;

                _root?.RemoveFromClassList("st-active-editing");
            }
        }

        protected void OnSceneGUI()
        {
            serializedObject.Update();

            var maybeQuat = GizmoQuaternion;
            if (maybeQuat == null) return;
            var quat = maybeQuat.Value;

            var center = _lastReferenceTransform.TransformPoint(_center.vector3Value);
            Handles.DrawWireDisc(center, _axis!.vector3Value, 0.2f, 4.0f);
            Handles.ArrowHandleCap(0, center, quat, 0.2f, EventType.Repaint);

            EditorGUI.BeginChangeCheck();
            quat = Handles.RotationHandle(quat, center);
            if (EditorGUI.EndChangeCheck())
            {
                GizmoQuaternion = quat;
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.BeginChangeCheck();
            center = Handles.PositionHandle(center, quat);
            if (EditorGUI.EndChangeCheck())
            {
                _center.vector3Value = _lastReferenceTransform.InverseTransformPoint(center);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private Transform? GetReferenceTransform()
        {
            var singleTarget = serializedObject.targetObject as VertexFilterByAxisComponent;
            if (singleTarget == null) return null;

            if (!singleTarget.TryGetComponent<ModularAvatarMeshCutter>(out var cutter)) return null;
            var obj = cutter.Object?.Get(cutter);
            if (obj == null) return null;
            if (!obj.TryGetComponent<Renderer>(out var renderer)) return null;

            if (renderer is SkinnedMeshRenderer smr && smr.rootBone != null) return smr.rootBone;
            return renderer.transform;
        }
    }
}