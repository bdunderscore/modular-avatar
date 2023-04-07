using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;
using static nadena.dev.modular_avatar.core.editor.Localization;
using Debug = System.Diagnostics.Debug;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarParametersRevalue))]
    internal class AvatarParameterReplacerEditor : MAEditorBase
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
            public List<ParameterMappingEntry> ImplicitParameters = new List<ParameterMappingEntry>();
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

            var target = (ModularAvatarParametersRevalue)this.target;
            var registered = target.parameters.Select(p => p.ParameterName)
                .ToImmutableHashSet();

            _implicitParams.ImplicitParameters.Clear();

            var detected = ParameterPolicy.ProbeParameters(((ModularAvatarParametersRevalue)target).gameObject);
            foreach (var param in detected.Values)
            {
                if (registered.Contains(param.MapKey)) continue;

                var config = new ParameterMappingEntry()
                {
                    ParameterName = param.OriginalName
                };

                _implicitParams.ImplicitParameters.Add(config);
                _selectedIndices.Add(-_implicitParams.ImplicitParameters.Count);
            }

            _implicitParamsObj.Update();
        }

        private void SetupList()
        {
            _parameters = serializedObject.FindProperty(nameof(ModularAvatarParametersRevalue.parameters));
            if (_devMode)
            {
                var target = (ModularAvatarParametersRevalue)this.target;

                _selectedIndices.Clear();
                _selectedIndices.AddRange(Enumerable.Range(0, target.parameters.Count));
            }
            else
            {
                _selectedIndices.Clear();
                for (int i = 0; i < _parameters.arraySize; i++)
                {
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

                var target = (ModularAvatarParametersRevalue)this.target;
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

        // https://github.com/bdunderscore/modular-avatar/issues/217
        private float[] ElementHeightCache = null;

        private float[] BuildHeightCache()
        {
            float[] cache = new float[_selectedIndices.Count];

            for (int i = 0; i < _selectedIndices.Count; i++)
            {
                float baseHeight = 0;

                var param = GetParamByIndex(_selectedIndices[i]);
                baseHeight += elemHeight;

                if (_selectedIndices[i] == -1)
                {
                    cache[i] = elemHeight + baseHeight;
                }
                else
                {
                    cache[i] = baseHeight;
                }
            }

            return cache;
        }

        private float ElementHeight(int reqIndex)
        {
            return ElementHeightCache[reqIndex];
        }

        private void DrawAutodetectHeader(ref Rect rect)
        {
            Rect top = rect;
            top.height = elemHeight;
            Rect bottom = rect;
            bottom.y += top.height;
            bottom.height -= top.height;
            rect = bottom;

            var style = new GUIStyle(EditorStyles.label);
            style.fontStyle = FontStyle.Italic;
            var content = new GUIContent(G("params_revalue.autodetect_header"));
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

            var parameterName = elem.FindPropertyRelative(nameof(ParameterMappingEntry.ParameterName));

            var margin = 20;
            var halfMargin = margin / 2;
            var leftHalf = new Rect(rect.x, rect.y, rect.width / 2 - halfMargin, elemHeight);
            var rightHalf = new Rect(rect.x + leftHalf.width + halfMargin, rect.y, leftHalf.width, elemHeight);
            var rightHalfTop = new Rect(rect.x + leftHalf.width + halfMargin, rect.y, leftHalf.width, elemHeight);
            var rightHalfSyncControlField = rightHalfTop;
            rightHalfSyncControlField.y += 0;

            var rightHalfDefaultValue = rightHalfSyncControlField;

            var indentLevel = EditorGUI.indentLevel;
            try
            {
                EditorGUI.BeginChangeCheck();

                indentLevel = 0;
                if (_devMode)
                {
                    EditorGUI.PropertyField(leftHalf, parameterName, GUIContent.none);
                }
                else
                {
                    EditorGUI.LabelField(leftHalf, parameterName.stringValue);
                }

                {
                    var defaultValueProp = elem.FindPropertyRelative(nameof(ParameterMappingEntry.OriginalValue));
                    var label = new GUIContent(G("params_revalue.original"));
                    var labelSize = EditorStyles.label.CalcSize(label);
                    var labelWidth = labelSize.x + EditorStyles.label.margin.right;

                    var labelRect = new Rect(rightHalfDefaultValue.x, rightHalfDefaultValue.y, labelWidth,
                        rightHalfDefaultValue.height);

                    EditorGUI.LabelField(labelRect, label);

                    rightHalfDefaultValue.x += labelWidth;
                    rightHalfDefaultValue.width = (rightHalfTop.width - (float)EditorStyles.label.margin.left) / 2 - labelWidth;

                    float val = (defaultValueProp.floatValue);
                    val = EditorGUI.FloatField(rightHalfDefaultValue, val);
                    defaultValueProp.floatValue = val;
                }
                
                {
                    var newValueProp = elem.FindPropertyRelative(nameof(ParameterMappingEntry.NewValue));
                    var label = new GUIContent(G("params_revalue.new"));
                    var labelSize = EditorStyles.label.CalcSize(label);
                    var labelWidth = labelSize.x + EditorStyles.label.margin.right;

                    rightHalfDefaultValue.x += rightHalfDefaultValue.width + EditorStyles.label.margin.left;
                    var labelRect = new Rect(rightHalfDefaultValue.x, rightHalfDefaultValue.y, labelWidth,
                        rightHalfDefaultValue.height);

                    EditorGUI.LabelField(labelRect, label);

                    rightHalfDefaultValue.x += labelWidth;
                    rightHalfDefaultValue.width = (rightHalfTop.width - (float)EditorStyles.label.margin.left) / 2 - labelWidth;

                    float val = (newValueProp.floatValue);
                    val = EditorGUI.FloatField(rightHalfDefaultValue, val);
                    newValueProp.floatValue = val;
                }

                if (EditorGUI.EndChangeCheck() && index < 0)
                {
                    var target = (ModularAvatarParametersRevalue)this.target;

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
            EditorGUI.LabelField(leftHalf, G("params_revalue.fieldname"));
            if (!_devMode) EditorGUI.LabelField(rightHalf, G("params_revalue.replacerto"));
        }

        protected override void OnInnerInspectorGUI()
        {
            ElementHeightCache = BuildHeightCache();
            EditorGUI.BeginChangeCheck();
            _devMode = EditorGUILayout.Toggle(G("params_revalue.devmode"), _devMode);
            if (EditorGUI.EndChangeCheck() || _reorderableList == null || _needsRebuild) SetupList();
            Debug.Assert(_reorderableList != null, nameof(_reorderableList) + " != null");
            if (_devMode || _selectedIndices.Count > 0)
            {
                _reorderableList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
            Localization.ShowLanguageUI();
        }
    }
}