﻿using System;
using System.Linq;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuItemCoreGUI
    {
        private static readonly ObjectIDGenerator IdGenerator = new ObjectIDGenerator();
        private readonly GameObject _parameterReference;
        private readonly Action _redraw;

        private readonly SerializedObject _obj;

        private readonly SerializedProperty _name;
        private readonly SerializedProperty _texture;
        private readonly SerializedProperty _type;
        private readonly SerializedProperty _value;
        private readonly SerializedProperty _submenu;

        private readonly ParameterGUI _parameterGUI;

        private readonly SerializedProperty _subParamsRoot;
        private readonly SerializedProperty _labelsRoot;

        private ParameterGUI[] _subParams;
        private SerializedProperty[] _labels;

        private int texPicker = -1;

        public MenuItemCoreGUI(SerializedObject obj, Action redraw)
        {
            _obj = obj;

            GameObject parameterReference = null;
            if (obj.targetObjects.Length == 1)
            {
                parameterReference = (obj.targetObject as Component)?.gameObject;
            }

            _parameterReference = parameterReference;
            _redraw = redraw;

            var gameObjects = new SerializedObject(
                obj.targetObjects.Select(o =>
                    (UnityEngine.Object) ((ModularAvatarMenuItem) o).gameObject
                ).ToArray()
            );

            _name = gameObjects.FindProperty("m_Name");
            var control = obj.FindProperty(nameof(ModularAvatarMenuItem.Control));

            _texture = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.icon));
            _type = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            var parameter = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            _value = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));
            _submenu = null;

            _parameterGUI = new ParameterGUI(parameterReference, parameter, redraw);

            _subParamsRoot = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subParameters));
            _labelsRoot = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.labels));
        }

        public MenuItemCoreGUI(GameObject parameterReference, SerializedProperty _control, Action redraw)
        {
            _obj = _control.serializedObject;
            _parameterReference = parameterReference;
            _redraw = redraw;
            _name = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.name));
            _texture = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.icon));
            _type = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            var parameter = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            _value = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));
            _submenu = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subMenu));

            _parameterGUI = new ParameterGUI(parameterReference, parameter, redraw);

            _subParamsRoot = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subParameters));
            _labelsRoot = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.labels));
        }

        public void DoGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.PropertyField(_name);
            EditorGUILayout.PropertyField(_texture);
            EditorGUILayout.PropertyField(_type);
            EditorGUILayout.PropertyField(_value);

            _parameterGUI.DoGUI();

            EditorGUILayout.EndVertical();

            if (_texture != null)
            {
                var tex = _texture.objectReferenceValue as Texture2D;
                if (tex != null)
                {
                    var size = EditorGUIUtility.singleLineHeight * 5;
                    var margin = 4;
                    var withMargin = new Vector2(margin + size, margin + size);

                    var rect = GUILayoutUtility.GetRect(withMargin.x, withMargin.y, GUILayout.ExpandWidth(false),
                        GUILayout.ExpandHeight(true));
                    rect.x += margin;
                    rect.y = rect.y + rect.height / 2 - size / 2;
                    rect.width = size;
                    rect.height = size;

                    GUI.Box(rect, new GUIContent(), "flow node 1");
                    GUI.DrawTexture(rect, tex);
                }
            }

            EditorGUILayout.EndHorizontal();

            try
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                if (_type.hasMultipleDifferentValues) return;
                VRCExpressionsMenu.Control.ControlType type =
                    (VRCExpressionsMenu.Control.ControlType) Enum
                        .GetValues(typeof(VRCExpressionsMenu.Control.ControlType))
                        .GetValue(_type.enumValueIndex);

                switch (type)
                {
                    case VRCExpressionsMenu.Control.ControlType.Button:
                    case VRCExpressionsMenu.Control.ControlType.Toggle:
                        break;
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                switch (type)
                {
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                        if (_submenu != null)
                        {
                            EditorGUILayout.PropertyField(_submenu);
                        }

                        break;
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    {
                        EnsureParameterCount(1);

                        _subParams[0].DoGUI(new GUIContent("Parameter Rotation"));

                        break;
                    }
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    {
                        EnsureParameterCount(2);
                        EnsureLabelCount(4);

                        EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2);

                        _subParams[0].DoGUI(new GUIContent("Parameter Horizontal"));
                        _subParams[1].DoGUI(new GUIContent("Parameter Vertical"));

                        DoFourAxisLabels(false);

                        break;
                    }

                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    {
                        DoFourAxisLabels(true);
                        break;
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                _obj.ApplyModifiedProperties();
            }
        }

        private void EnsureLabelCount(int i)
        {
            if (_labels == null || _labelsRoot.arraySize < i || _labels.Length < i)
            {
                _labelsRoot.arraySize = i;
                _labels = new SerializedProperty[i];
                for (int j = 0; j < i; j++)
                {
                    _labels[j] = _labelsRoot.GetArrayElementAtIndex(j);
                }
            }
        }

        private void CenterLabel(Rect rect, GUIContent content, GUIStyle style)
        {
            var size = style.CalcSize(content);
            var x = rect.x + rect.width / 2 - size.x / 2;
            var y = rect.y + rect.height / 2 - size.y / 2;
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
        }

        private void DoFourAxisLabels(bool showParams)
        {
            float maxWidth = 128 * 3;

            EnsureLabelCount(4);
            if (showParams) EnsureParameterCount(4);

            float extraHeight = EditorGUIUtility.singleLineHeight * 3;
            if (showParams) extraHeight += EditorGUIUtility.singleLineHeight;

            EditorGUILayout.LabelField(showParams ? "Control Labels and Parameters" : "Control Labels",
                EditorStyles.boldLabel);

            var square = GUILayoutUtility.GetAspectRect(1, GUILayout.MaxWidth(maxWidth));
            var extraSpace = GUILayoutUtility.GetRect(0, 0, extraHeight,
                extraHeight, GUILayout.ExpandWidth(true));

            var rect = square;
            rect.height += extraSpace.height;

            float extraWidth = Math.Max(0, extraSpace.width - rect.width);
            rect.x += extraWidth / 2;

            var blockHeight = rect.height / 3;
            var blockWidth = rect.width / 3;

            var up = rect;
            up.yMax -= blockHeight * 2;
            up.xMin += blockWidth;
            up.xMax -= blockWidth;

            var down = rect;
            down.yMin += blockHeight * 2;
            down.xMin += blockWidth;
            down.xMax -= blockWidth;

            var left = rect;
            left.yMin += blockHeight;
            left.yMax -= blockHeight;
            left.xMax -= blockWidth * 2;

            var right = rect;
            right.yMin += blockHeight;
            right.yMax -= blockHeight;
            right.xMin += blockWidth * 2;

            var center = rect;
            center.yMin += blockHeight;
            center.yMax -= blockHeight;
            center.xMin += blockWidth;
            center.xMax -= blockWidth;

            SingleLabel(0, up);
            SingleLabel(1, right);
            SingleLabel(2, down);
            SingleLabel(3, left);

            var rect_param_l = center;
            rect_param_l.yMin = rect_param_l.yMax - EditorGUIUtility.singleLineHeight;
            var rect_name_l = rect_param_l;
            if (showParams) rect_name_l.y -= rect_param_l.height;

            if (showParams) CenterLabel(rect_param_l, new GUIContent("Parameter"), EditorStyles.label);
            CenterLabel(rect_name_l, new GUIContent("Label"), EditorStyles.label);

            void SingleLabel(int index, Rect block)
            {
                var prop_name = _labels[index].FindPropertyRelative(nameof(VRCExpressionsMenu.Control.Label.name));
                var prop_icon = _labels[index].FindPropertyRelative(nameof(VRCExpressionsMenu.Control.Label.icon));

                var rect_param = block;
                rect_param.yMin = rect_param.yMax - EditorGUIUtility.singleLineHeight;

                var rect_name = rect_param;
                if (showParams) rect_name.y -= rect_param.height;

                var rect_icon = block;
                rect_icon.yMax = rect_name.yMin;

                EditorGUI.PropertyField(rect_name, prop_name, GUIContent.none);
                if (showParams)
                {
                    _subParams[index].DoGUI(rect_param, GUIContent.none);
                }

                var tex = prop_icon.objectReferenceValue as Texture;
                GUIContent icon_content;

                if (prop_icon.hasMultipleDifferentValues)
                {
                    icon_content = new GUIContent("(multiple)");
                }
                else
                {
                    icon_content = tex != null ? new GUIContent(tex) : new GUIContent("(no icon)");
                }

                int objectId = GUIUtility.GetControlID(
                    ((int) IdGenerator.GetId(this, out bool _) << 2) | index,
                    FocusType.Passive,
                    block
                );

                if (GUI.Button(rect_icon, icon_content))
                {
                    texPicker = index;

                    EditorGUIUtility.ShowObjectPicker<Texture2D>(
                        prop_icon.hasMultipleDifferentValues ? null : prop_icon.objectReferenceValue, false,
                        "t:texture2d", objectId);
                }

                if (texPicker == index)
                {
                    if (Event.current.commandName == "ObjectSelectorUpdated" &&
                        EditorGUIUtility.GetObjectPickerControlID() == objectId)
                    {
                        prop_icon.objectReferenceValue = EditorGUIUtility.GetObjectPickerObject() as Texture;
                        _redraw();
                    }
                }
            }
        }

        private void EnsureParameterCount(int i)
        {
            if (_subParams == null || _subParamsRoot.arraySize < i || _subParams.Length < i)
            {
                _subParamsRoot.arraySize = i;
                _subParams = new ParameterGUI[i];
                for (int j = 0; j < i; j++)
                {
                    var prop = _subParamsRoot.GetArrayElementAtIndex(j)
                        .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.Parameter.name));
                    _subParams[j] = new ParameterGUI(_parameterReference, prop, _redraw);
                }
            }
        }
    }
}