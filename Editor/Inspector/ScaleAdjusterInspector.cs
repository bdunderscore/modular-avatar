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
        private uint _lastScaleHash;
        private bool _lastScaleHasMultipleDifferentValues;
        private Vector3[] _lastScales;
        private bool _undoRedoPerformed;

        protected void OnEnable()
        {
            _scale = serializedObject.FindProperty("m_Scale");
            _lastScaleHash = _scale.contentHash;
            _lastScaleHasMultipleDifferentValues = _scale.hasMultipleDifferentValues;
            _lastScales = targets.OfType<ModularAvatarScaleAdjuster>().Select(s => s.Scale).ToArray();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            _undoRedoPerformed = true;
        }

        protected override void OnDestroy()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            base.OnDestroy();
        }

        protected override void OnInnerInspectorGUI()
        {
            var adjusters = targets.OfType<ModularAvatarScaleAdjuster>().ToArray();
            var currentScales = adjusters.Select(s => s.Scale).ToArray();

            var undoRedoPerformed = _undoRedoPerformed;
            _undoRedoPerformed = false;
            if (undoRedoPerformed)
            {
                serializedObject.Update();
            }

            var externallyChanged = !undoRedoPerformed && (_lastScales == null
                                    || _lastScales.Length != currentScales.Length
                                    || currentScales.Zip(_lastScales, (current, last) => current != last).Any(changed => changed));

            var scalers = adjusters
                .Select(s => new ScaleAdjusterTool.AdjusterScaler(s)).ToList();

            var previousScaleHash = _lastScaleHash;
            var previousScaleHasMultipleDifferentValues = _lastScaleHasMultipleDifferentValues;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_scale, G("scale_adjuster.scale"));

            var guiChanged = EditorGUI.EndChangeCheck();
            var scaleChanged = !undoRedoPerformed && (guiChanged
                            || _scale.contentHash != previousScaleHash
                            || _scale.hasMultipleDifferentValues != previousScaleHasMultipleDifferentValues
                            || externallyChanged);

            if (scaleChanged)
            {
                for (var i = 0; i < scalers.Count; i++)
                {
                    // If the scale was changed externally (e.g., by another script or inspector), we want to reset the scale to the last known value.
                    if (externallyChanged && _lastScales != null && i < _lastScales.Length)
                    {
                        scalers[i].SetScale(_scale.vector3Value, _lastScales[i]);
                    }
                    else
                    {
                        scalers[i].Scale = _scale.vector3Value;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            _lastScaleHash = _scale.contentHash;
            _lastScaleHasMultipleDifferentValues = _scale.hasMultipleDifferentValues;
            _lastScales = adjusters.Select(s => s.Scale).ToArray();

            ScaleAdjusterTool.AdjustChildPositions = EditorGUILayout.Toggle(G("scale_adjuster.adjust_children"),
                ScaleAdjusterTool.AdjustChildPositions);

            ShowLanguageUI();
        }
    }
}