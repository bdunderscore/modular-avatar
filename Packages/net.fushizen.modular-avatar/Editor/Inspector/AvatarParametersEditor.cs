using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;

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

        private bool _needsRebuild = false;
        private float elemHeight;

        private readonly List<int> _selectedIndices = new List<int>();
        private ImplicitParams _implicitParams = null;
        private SerializedObject _implicitParamsObj;
        private SerializedProperty _implicitParamsProp;

        private class ImplicitParams : ScriptableObject
        {
            public List<ParameterConfig> ImplicitParameters = new List<ParameterConfig>();
        }

        private void OnEnable()
        {
            SetupList();
        }

        private SerializedProperty GetParamByIndex(int idx)
        {
            if (idx >= 0)
            {
                return _parameters.GetArrayElementAtIndex(idx);
            }
            else
            {
                idx = -(idx + 1);
                return _implicitParamsProp.GetArrayElementAtIndex(idx);
            }
        }

        private void DetectParameters()
        {
            if (_implicitParams == null || _implicitParamsProp == null)
            {
                _implicitParams = CreateInstance<ImplicitParams>();
                _implicitParamsObj = new SerializedObject(_implicitParams);
                _implicitParamsProp = _implicitParamsObj.FindProperty(nameof(ImplicitParams.ImplicitParameters));
            }

            var target = (ModularAvatarParameters) this.target;
            var registered = target.parameters.Select(p => p.nameOrPrefix + (p.isPrefix ? "*" : ""))
                .ToImmutableHashSet();

            _implicitParams.ImplicitParameters.Clear();

            var detected = ParameterPolicy.ProbeParameters(((ModularAvatarParameters) target).gameObject);
            foreach (var param in detected.Values)
            {
                if (registered.Contains(param.MapKey)) continue;

                var config = new ParameterConfig()
                {
                    internalParameter = false,
                    isPrefix = param.IsPrefix,
                    nameOrPrefix = param.OriginalName,
                    remapTo = "",
                };

                _implicitParams.ImplicitParameters.Add(config);
                _selectedIndices.Add(-_implicitParams.ImplicitParameters.Count);
            }

            _implicitParamsObj.Update();
        }

        private void SetupList()
        {
            _parameters = serializedObject.FindProperty(nameof(ModularAvatarParameters.parameters));
            if (_devMode)
            {
                var target = (ModularAvatarParameters) this.target;

                _selectedIndices.Clear();
                _selectedIndices.AddRange(Enumerable.Range(0, target.parameters.Count));
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
            }

            DetectParameters();

            if (_reorderableList == null)
            {
                _reorderableList = new ReorderableList(
                    _selectedIndices,
                    typeof(int),
                    false, true, _devMode, _devMode
                );
                _reorderableList.drawHeaderCallback = DrawHeader;
                _reorderableList.drawElementCallback = DrawElement;
                _reorderableList.onAddCallback = AddElement;
                _reorderableList.onRemoveCallback = RemoveElement;
                _reorderableList.elementHeightCallback = ElementHeight;
                _reorderableList.onReorderCallbackWithDetails = ReorderElements;
                elemHeight = _reorderableList.elementHeight;
            }

            _reorderableList.displayAdd = _devMode;
            _reorderableList.displayRemove = _devMode;
            _reorderableList.draggable = _devMode;

            _needsRebuild = false;
        }

        private void ReorderElements(ReorderableList list, int oldindex, int newindex)
        {
            if (_selectedIndices[oldindex] >= 0 && _selectedIndices[newindex] >= 0)
            {
                // We're in dev mode, so the "real" entries are in the same order in _selectedIndices
                // as the underlying. So just reorder them in the underlying object.
                serializedObject.ApplyModifiedProperties();

                var target = (ModularAvatarParameters) this.target;
                var tmp = target.parameters[oldindex];
                target.parameters.RemoveAt(oldindex);
                target.parameters.Insert(newindex, tmp);

                serializedObject.Update();
            }

            // The reorderable list trashed our internal indices, so force a rebuild on next repaint.
            _needsRebuild = true;
        }

        private void AddElement(ReorderableList list)
        {
            _parameters.arraySize += 1;
            _selectedIndices.Insert(_parameters.arraySize - 1, _parameters.arraySize - 1);
            list.index = _parameters.arraySize - 1;
        }

        private void RemoveElement(ReorderableList list)
        {
            if (list.index < 0 || _selectedIndices[list.index] < 0) return;
            _parameters.DeleteArrayElementAtIndex(list.index);

            _needsRebuild = true;
        }

        private float ElementHeight(int index)
        {
            if (_selectedIndices[index] == -1)
            {
                return elemHeight * 2;
            }
            else
            {
                return elemHeight;
            }
        }

        private void DrawAutodetectHeader(ref Rect rect)
        {
            Rect top = rect;
            top.height /= 2;
            Rect bottom = rect;
            bottom.y += top.height;
            bottom.height -= top.height;
            rect = bottom;

            var style = new GUIStyle(EditorStyles.label);
            style.fontStyle = FontStyle.Italic;
            var content = new GUIContent("   Autodetected Parameters   ");
            var size = style.CalcSize(content);

            var centeredRect = new Rect(
                top.x + (top.width - size.x) / 2,
                top.y + (top.height - size.y) / 2,
                size.x,
                size.y
            );

            EditorGUI.LabelField(centeredRect, content, style);
        }

        private void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var originalIndex = index;
            index = _selectedIndices[index];
            var elem = GetParamByIndex(index);

            if (index == -1)
            {
                DrawAutodetectHeader(ref rect);
            }

            var margin = 20;
            var halfMargin = margin / 2;
            var leftHalf = new Rect(rect.x, rect.y, rect.width / 2 - halfMargin, rect.height);
            var rightHalf = new Rect(rect.x + leftHalf.width + halfMargin, rect.y, leftHalf.width, rect.height);

            var nameOrPrefix = elem.FindPropertyRelative(nameof(ParameterConfig.nameOrPrefix));
            var remapTo = elem.FindPropertyRelative(nameof(ParameterConfig.remapTo));
            var internalParameter = elem.FindPropertyRelative(nameof(ParameterConfig.internalParameter));
            var isPrefix = elem.FindPropertyRelative(nameof(ParameterConfig.isPrefix));

            var indentLevel = EditorGUI.indentLevel;
            try
            {
                EditorGUI.BeginChangeCheck();

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

                    if (string.IsNullOrWhiteSpace(remapTo.stringValue))
                    {
                        var style = new GUIStyle(EditorStyles.label);
                        style.fontStyle = FontStyle.Italic;
                        var oldColor = GUI.color;
                        var color = GUI.color;
                        color.a = 0.5f;
                        GUI.color = color;

                        EditorGUI.LabelField(rightHalf, nameOrPrefix.stringValue, style);

                        GUI.color = oldColor;
                    }
                }

                if (EditorGUI.EndChangeCheck() && index < 0)
                {
                    var target = (ModularAvatarParameters) this.target;

                    // Create this implicit parameter in the backing object
                    var implicitIndex = -(index + 1);

                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(target, "Updating parameters");
                    _implicitParamsObj.ApplyModifiedPropertiesWithoutUndo();

                    var config = _implicitParams.ImplicitParameters[implicitIndex];
                    target.parameters.Add(config);
                    _selectedIndices.RemoveAt(originalIndex);
                    _selectedIndices.Insert(target.parameters.Count - 1, target.parameters.Count - 1);

                    serializedObject.Update();
                    _reorderableList.index = target.parameters.Count - 1;

                    _needsRebuild = true;
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
            if (EditorGUI.EndChangeCheck() || _reorderableList == null || _needsRebuild) SetupList();
            _reorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }
    }
}