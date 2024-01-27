#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;
using Debug = System.Diagnostics.Debug;

namespace nadena.dev.modular_avatar.core.editor
{
#if !UNITY_2022_1_OR_NEWER
    [CustomEditor(typeof(ModularAvatarParameters))]
#endif
    internal class AvatarParametersEditor2019 : MAEditorBase
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
                    syncType = param.syncType,
                    defaultValue = param.defaultValue,
                    saved = param.saved,
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

            ElementHeightCache = BuildHeightCache();

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

        // https://github.com/bdunderscore/modular-avatar/issues/217
        private float[] ElementHeightCache = null;

        private float[] BuildHeightCache()
        {
            float[] cache = new float[_selectedIndices.Count];

            for (int i = 0; i < _selectedIndices.Count; i++)
            {
                float baseHeight = _devMode ? elemHeight * 2 : elemHeight;

                var param = GetParamByIndex(_selectedIndices[i]);
                var syncMode = param.FindPropertyRelative(nameof(ParameterConfig.syncType));
                if (syncMode.enumValueIndex != (int) ParameterSyncType.NotSynced)
                {
                    baseHeight += elemHeight;
                }

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
            var content = new GUIContent(G("params.autodetect_header"));
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

            var nameOrPrefix = elem.FindPropertyRelative(nameof(ParameterConfig.nameOrPrefix));
            var remapTo = elem.FindPropertyRelative(nameof(ParameterConfig.remapTo));
            var internalParameter = elem.FindPropertyRelative(nameof(ParameterConfig.internalParameter));
            var isPrefix = elem.FindPropertyRelative(nameof(ParameterConfig.isPrefix));
            var syncType = elem.FindPropertyRelative(nameof(ParameterConfig.syncType));

            var isSynced = syncType.enumValueIndex != (int) ParameterSyncType.NotSynced;

            var margin = 20;
            var halfMargin = margin / 2;
            var leftHalf = new Rect(rect.x, rect.y, rect.width * 0.3f - halfMargin, elemHeight);
            var rightHalf = new Rect(rect.x + leftHalf.width + halfMargin, rect.y, leftHalf.width, elemHeight);
            rightHalf.xMax = rect.xMax;
            var rightHalfTop = new Rect(rect.x + leftHalf.width + halfMargin, rect.y, leftHalf.width, elemHeight);
            rightHalfTop.xMax = rect.xMax;
            var rightHalfSyncControlField = rightHalfTop;
            rightHalfSyncControlField.y += _devMode ? elemHeight : 0;

            var rightHalfDefaultValue = rightHalfSyncControlField;
            rightHalfDefaultValue.y += elemHeight;

            var indentLevel = EditorGUI.indentLevel;
            try
            {
                EditorGUI.BeginChangeCheck();

                indentLevel = 0;
                if (_devMode)
                {
                    EditorGUI.PropertyField(leftHalf, nameOrPrefix, GUIContent.none);

                    var internalGuiContent = G("params.internal");
                    var toggleInternalWidth = EditorStyles.toggle.CalcSize(internalGuiContent).x;
                    var toggleInternalRect = new Rect(rightHalfTop.x, rightHalfTop.y, toggleInternalWidth,
                        rightHalfTop.height);

                    internalParameter.boolValue =
                        EditorGUI.ToggleLeft(toggleInternalRect, internalGuiContent, internalParameter.boolValue);

                    var isPrefixRect = new Rect(rightHalfTop.x + toggleInternalWidth + halfMargin, rightHalfTop.y,
                        rightHalfTop.width - toggleInternalWidth - halfMargin, rightHalfTop.height);
                    isPrefix.boolValue = EditorGUI.ToggleLeft(isPrefixRect, G("params.pb_prefix"), isPrefix.boolValue);

                    var syncedContent = new GUIContent(G("params.syncmode"));
                    var labelSize = EditorStyles.label.CalcSize(syncedContent);
                    var syncedWidth = labelSize.x + EditorStyles.label.margin.right;

                    var syncedRect = new Rect(rightHalfSyncControlField.x, rightHalfSyncControlField.y, syncedWidth,
                        rightHalfSyncControlField.height);

                    EditorGUI.LabelField(syncedRect, syncedContent);

                    rightHalfSyncControlField.x += syncedWidth;
                    rightHalfSyncControlField.width -= syncedWidth;

                    EditorGUI.PropertyField(rightHalfSyncControlField, syncType, GUIContent.none);
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

                if (isSynced)
                {
                    var localOnly = elem.FindPropertyRelative(nameof(ParameterConfig.localOnly));
                    var saved = elem.FindPropertyRelative(nameof(ParameterConfig.saved));

                    var savedContents = new GUIContent(G("params.saved"));
                    var savedStyle = EditorStyles.toggle;
                    var savedSize = savedStyle.CalcSize(savedContents);
                    var savedLabelWidth = EditorStyles.label.CalcSize(savedContents).x;
                    var checkboxPad = EditorStyles.toggle.margin.right;

                    var savedPos = rightHalfDefaultValue;
                    savedPos.width = savedSize.x + checkboxPad * 2;
                    rightHalfDefaultValue.x += savedPos.width;
                    rightHalfDefaultValue.width -= savedPos.width;
                    //savedPos.x -= savedSize.x + checkboxPad * 2;

                    EditorGUI.LabelField(savedPos, savedContents);
                    savedPos.x += savedLabelWidth + checkboxPad;
                    savedPos.width -= savedLabelWidth - checkboxPad * 2;
                    saved.boolValue = EditorGUI.Toggle(savedPos, saved.boolValue);

                    var syncedContents = new GUIContent(G("params.synced"));
                    var syncedStyle = EditorStyles.toggle;
                    var syncedSize = syncedStyle.CalcSize(syncedContents);
                    var syncedLabelWidth = EditorStyles.label.CalcSize(syncedContents).x;

                    var syncedPos = rightHalfDefaultValue;
                    syncedPos.width = syncedSize.x + checkboxPad * 2;
                    rightHalfDefaultValue.x += syncedPos.width;
                    rightHalfDefaultValue.width -= syncedPos.width;

                    EditorGUI.LabelField(syncedPos, syncedContents);
                    syncedPos.x += syncedLabelWidth + checkboxPad;
                    syncedPos.width -= syncedLabelWidth - checkboxPad * 2;
                    localOnly.boolValue = !EditorGUI.Toggle(syncedPos, !localOnly.boolValue);

                    var defaultValueProp = elem.FindPropertyRelative(nameof(ParameterConfig.defaultValue));
                    var label = new GUIContent(G("params.default"));
                    var labelSize = EditorStyles.label.CalcSize(label);
                    var labelWidth = labelSize.x + EditorStyles.label.margin.right;

                    var labelRect = new Rect(rightHalfDefaultValue.x, rightHalfDefaultValue.y, labelWidth,
                        rightHalfDefaultValue.height);

                    EditorGUI.LabelField(labelRect, label);

                    rightHalfDefaultValue.x += labelWidth;
                    rightHalfDefaultValue.width -= labelWidth;

                    switch ((ParameterSyncType) syncType.enumValueIndex)
                    {
                        case ParameterSyncType.Int:
                        {
                            int val = Mathf.RoundToInt(defaultValueProp.floatValue);
                            val = EditorGUI.IntField(rightHalfDefaultValue, val);
                            defaultValueProp.floatValue = val;
                            break;
                        }
                        case ParameterSyncType.Float:
                        {
                            float val = defaultValueProp.floatValue;
                            val = EditorGUI.FloatField(rightHalfDefaultValue, val);
                            defaultValueProp.floatValue = val;
                            break;
                        }

                        case ParameterSyncType.Bool:
                        {
                            bool val = defaultValueProp.floatValue > 0.1f;
                            val = EditorGUI.Toggle(rightHalfDefaultValue, val);
                            defaultValueProp.floatValue = val ? 1.0f : 0.0f;
                            break;
                        }

                        default:
                            // Maybe we just changed sync mode?
                            break;
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
            var leftHalf = new Rect(rect.x, rect.y, rect.width * 0.3f, rect.height);
            var rightHalf = new Rect(rect.x + leftHalf.width, rect.y, rect.width / 2, rect.height);

            EditorGUI.LabelField(leftHalf, G("params.fieldname"));
            if (!_devMode) EditorGUI.LabelField(rightHalf, G("params.remapto"));
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            _devMode = EditorGUILayout.Toggle(G("params.devmode"), _devMode);
            if (EditorGUI.EndChangeCheck() || _reorderableList == null || _needsRebuild) SetupList();
            Debug.Assert(_reorderableList != null, nameof(_reorderableList) + " != null");
            
            ElementHeightCache = BuildHeightCache();

            if (_devMode || _selectedIndices.Count > 0)
            {
                _reorderableList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();

            Localization.ShowLanguageUI();
        }
    }
}

#endif