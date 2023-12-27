using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarScaleAdjuster))]
    [CanEditMultipleObjects]
    internal class ScaleAdjusterInspector : MAEditorBase
    {
        private SerializedProperty _scale;

        private ModularAvatarScaleAdjuster[] _sortedTargets;
        private Vector3[] _originalScales;

        private Vector3 gizmoScale = Vector3.one;

        private bool _adjustChildPositions;

        protected void OnEnable()
        {
            _scale = serializedObject.FindProperty("m_Scale");

            _sortedTargets = targets.Cast<ModularAvatarScaleAdjuster>().OrderBy(TransformDepth).ToArray();
            _originalScales = _sortedTargets.Select(t => t.Scale).ToArray();
        }

        private int TransformDepth(ModularAvatarScaleAdjuster obj)
        {
            var t = obj.transform;
            var depth = 0;

            while (t != null)
            {
                depth++;
                t = t.parent;
            }

            return depth;
        }

        protected void OnDisable()
        {
        }

        public void OnSceneGUI()
        {
            Selection.selectionChanged -= UnhideTools;
            Selection.selectionChanged += UnhideTools;
            Tools.hidden = (Tools.current == Tool.Scale);
            if (!Tools.hidden) return;

            var handlePos = _sortedTargets[0].transform;
            EditorGUI.BeginChangeCheck();
            var handleSize = HandleUtility.GetHandleSize(handlePos.position);
            gizmoScale = Handles.ScaleHandle(gizmoScale, handlePos.position, handlePos.rotation, handleSize);
            if (EditorGUI.EndChangeCheck())
            {
                for (int i = 0; i < _sortedTargets.Length; i++)
                {
                    UpdateScale(i, handlePos);
                }
            }
        }

        private void UpdateScale(int i, Transform refTransform)
        {
            var xform = _sortedTargets[i].transform;
            var target = _sortedTargets[i];

            Matrix4x4 initialTransform = xform.parent.localToWorldMatrix * Matrix4x4.TRS(
                xform.localPosition,
                xform.localRotation,
                xform.localScale
            );

            Matrix4x4 initialScale = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                _originalScales[i]
            );

            Matrix4x4 newTransform = refTransform.localToWorldMatrix * Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                gizmoScale
            );

            float scaleX = TransformVec(Vector3.right);
            float scaleY = TransformVec(Vector3.up);
            float scaleZ = TransformVec(Vector3.forward);

            Undo.RecordObject(target, "Adjust scale");
            var targetL2W = target.transform.localToWorldMatrix;
            var baseToScaleCoord = (targetL2W * Matrix4x4.Scale(target.Scale)).inverse * targetL2W;

            target.Scale = new Vector3(scaleX, scaleY, scaleZ);

            var scaleToBaseCoord = Matrix4x4.Scale(target.Scale);

            PrefabUtility.RecordPrefabInstancePropertyModifications(target);

            // Update child positions
            if (_adjustChildPositions)
            {
                var updateTransform = scaleToBaseCoord * baseToScaleCoord;
                foreach (Transform child in target.transform)
                {
                    Undo.RecordObject(child, "Adjust scale");
                    child.localPosition = updateTransform.MultiplyPoint(child.localPosition);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                }
            }

            float TransformVec(Vector3 vec)
            {
                // first, place our measurement vector into world spoce
                vec = (initialTransform * initialScale).MultiplyVector(vec);
                // now put it into reference space
                vec = refTransform.worldToLocalMatrix.MultiplyVector(vec);
                // and return using the adjusted scale
                vec = newTransform.MultiplyVector(vec);
                // now come back into local space
                vec = (initialTransform.inverse).MultiplyVector(vec);

                return vec.magnitude;
            }
        }

        private static void UnhideTools()
        {
            Tools.hidden = false;
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.PropertyField(_scale, G("scale_adjuster.scale"));

            _adjustChildPositions = EditorGUILayout.Toggle(G("scale_adjuster.adjust_children"), _adjustChildPositions);

            serializedObject.ApplyModifiedProperties();

            Localization.ShowLanguageUI();
        }
    }
}