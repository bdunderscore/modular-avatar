using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarParameters))]
    public class AvatarParametersEditor : Editor
    {
        /**********************************************************************
         * | Field name | Remap to / config  |
         * |------------|--------------------|
         */

        private bool _devMode;
        private ReorderableList _reorderableList;
        private SerializedProperty _parameters;

        private readonly List<int> _selectedIndices = new List<int>();

        private void OnEnable()
        {
            SetupList();
        }

        private void SetupList()
        {
            _parameters = serializedObject.FindProperty(nameof(ModularAvatarParameters.parameters));
            if (_devMode)
            {
                _reorderableList = new ReorderableList(
                    serializedObject,
                    _parameters,
                    true, true, true, true
                );
                _reorderableList.drawHeaderCallback = DrawHeader;
                _reorderableList.drawElementCallback = DrawElement;
                _reorderableList.onAddCallback = AddElement;
                _reorderableList.onRemoveCallback = RemoveElement;
            }
            else
            {
                _selectedIndices.Clear();
                for (int i = 0; i < _parameters.arraySize; i++)
                {
                    var isInternal = _parameters.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(ParameterConfig.internalParameter));
                    if (isInternal.boolValue) continue;

                    _selectedIndices.Add(i);
                }

                _reorderableList = new ReorderableList(
                    _selectedIndices,
                    typeof(int),
                    false, true, false, false
                );
                _reorderableList.drawHeaderCallback = DrawHeader;
                _reorderableList.drawElementCallback = DrawElement;
                _reorderableList.onAddCallback = AddElement;
                _reorderableList.onRemoveCallback = RemoveElement;
            }
        }

        private void AddElement(ReorderableList list)
        {
            _parameters.arraySize += 1;
            list.index = _parameters.arraySize - 1;
        }

        private void RemoveElement(ReorderableList list)
        {
            if (list.index < 0) return;
            _parameters.DeleteArrayElementAtIndex(list.index);
        }

        private void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var margin = 20;
            var halfMargin = margin / 2;
            var leftHalf = new Rect(rect.x, rect.y, rect.width / 2 - halfMargin, rect.height);
            var rightHalf = new Rect(rect.x + leftHalf.width + halfMargin, rect.y, leftHalf.width, rect.height);

            if (!_devMode) index = _selectedIndices[index];

            var elem = _parameters.GetArrayElementAtIndex(index);

            var nameOrPrefix = elem.FindPropertyRelative(nameof(ParameterConfig.nameOrPrefix));
            var remapTo = elem.FindPropertyRelative(nameof(ParameterConfig.remapTo));
            var internalParameter = elem.FindPropertyRelative(nameof(ParameterConfig.internalParameter));
            var isPrefix = elem.FindPropertyRelative(nameof(ParameterConfig.isPrefix));

            var indentLevel = EditorGUI.indentLevel;
            try
            {
                indentLevel = 0;
                if (_devMode)
                {
                    EditorGUI.PropertyField(leftHalf, nameOrPrefix, GUIContent.none);

                    var toggleInternalWidth = EditorStyles.toggle.CalcSize(new GUIContent("Internal")).x;
                    var toggleInternalRect = new Rect(rightHalf.x, rightHalf.y, toggleInternalWidth, rightHalf.height);

                    internalParameter.boolValue =
                        EditorGUI.ToggleLeft(toggleInternalRect, "Internal", internalParameter.boolValue);

                    var isPrefixRect = new Rect(rightHalf.x + toggleInternalWidth + halfMargin, rightHalf.y,
                        rightHalf.width - toggleInternalWidth - halfMargin, rightHalf.height);
                    isPrefix.boolValue = EditorGUI.ToggleLeft(isPrefixRect, "PhysBones Prefix", isPrefix.boolValue);
                }
                else
                {
                    EditorGUI.LabelField(leftHalf,
                        isPrefix.boolValue ? nameOrPrefix.stringValue + "*" : nameOrPrefix.stringValue);
                    EditorGUI.PropertyField(rightHalf, remapTo, GUIContent.none);
                }
            }
            finally
            {
                EditorGUI.indentLevel = indentLevel;
            }
        }

        private void DrawHeader(Rect rect)
        {
            var leftHalf = new Rect(rect.x, rect.y, rect.width / 2, rect.height);
            var rightHalf = new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);

            EditorGUI.LabelField(leftHalf, "Field name");
            if (!_devMode) EditorGUI.LabelField(rightHalf, "Remap to");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            _devMode = EditorGUILayout.Toggle("Developer mode", _devMode);
            if (EditorGUI.EndChangeCheck()) SetupList();
            _reorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }
    }
}