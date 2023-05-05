using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ActionBlendshape))]
    internal class ActionBlendshapeEditor : MAEditorBase
    {
        private ColumnReorderableList _list;
        private SerializedProperty _listProp;
        private Dictionary<Mesh, string[]> _blendshapeNamesCache = new Dictionary<Mesh, string[]>();
        private Transform _avatarRoot;

        private void OnEnable()
        {
            _listProp = serializedObject.FindProperty(nameof(ActionBlendshape.Blendshapes));
            _list = new ColumnReorderableList(
                serializedObject,
                _listProp,
                true,
                true,
                true,
                true
            );

            _list.Repaint = Repaint;
            _list.OnGenerateColumns = BuildColumns;
            _list.onRemoveCallback += OnRemoveElement;
            _list.onAddCallback += OnAddElement;

            _list.elementHeight += 2;

            if (targets.Length == 1)
            {
                _avatarRoot = RuntimeUtil.FindAvatarInParents(((Component) target).transform).transform;
            }
        }

        private void OnAddElement(ReorderableList list)
        {
            _listProp.arraySize++;
        }

        private void OnRemoveElement(ReorderableList reorderableList)
        {
            if (reorderableList.index < _listProp.arraySize)
            {
                _listProp.DeleteArrayElementAtIndex(reorderableList.index);
            }
        }

        private void RenderBlendshapeColumn(Rect rect, SerializedProperty elem)
        {
            var prop_obj = elem.FindPropertyRelative(nameof(ActionBlendshape.BlendshapeSpec.target))
                ?.FindPropertyRelative(nameof(AvatarObjectReference.referencePath));
            var prop_blendshape = elem.FindPropertyRelative(nameof(ActionBlendshape.BlendshapeSpec.blendshape));

            SkinnedMeshRenderer targetRenderer = null;

            if (_avatarRoot != null)
            {
                var targetObj = _avatarRoot.Find(prop_obj.stringValue);
                if (targetObj != null)
                {
                    targetRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();
                }
            }

            if (targetRenderer == null || targetRenderer.sharedMesh == null)
            {
                EditorGUI.PropertyField(rect, prop_blendshape, GUIContent.none);
                return;
            }

            var sharedMesh = targetRenderer.sharedMesh;

            if (!_blendshapeNamesCache.TryGetValue(sharedMesh, out var names))
            {
                names = new string[sharedMesh.blendShapeCount + 1];
                names[0] = "";
                for (var i = 1; i < sharedMesh.blendShapeCount; i++)
                {
                    names[i] = sharedMesh.GetBlendShapeName(i - 1);
                }

                _blendshapeNamesCache[sharedMesh] = names;
            }

            var blendshapeIndex = Array.IndexOf(names, prop_blendshape.stringValue);
            var style = new GUIStyle(EditorStyles.popup);

            EditorGUI.BeginChangeCheck();
            blendshapeIndex = EditorGUI.Popup(rect, blendshapeIndex, names, style);
            if (EditorGUI.EndChangeCheck())
            {
                prop_blendshape.stringValue = names[blendshapeIndex];
            }
            else if (blendshapeIndex < 0)
            {
                var toDisplay = prop_blendshape.stringValue;

                UpdateAllStates(style, s => s.textColor = Color.Lerp(s.textColor, Color.red, 0.85f));

                GUI.Label(rect, toDisplay, style);
            }
        }

        private static void UpdateAllStates(GUIStyle style, Action<GUIStyleState> action)
        {
            var state = style.normal;
            action(state);
            style.normal = state;

            state = style.hover;
            action(state);
            style.hover = state;

            state = style.active;
            action(state);
            style.active = state;

            state = style.focused;
            action(state);
            style.focused = state;

            state = style.onNormal;
            action(state);
            style.onNormal = state;

            state = style.onHover;
            action(state);
            style.onHover = state;

            state = style.onActive;
            action(state);
            style.onActive = state;

            state = style.onFocused;
            action(state);
            style.onFocused = state;
        }

        private void BuildColumns(float width)
        {
            GUIContent testValueContent = new GUIContent("100");
            var valueFieldSize = EditorStyles.textField.CalcSize(testValueContent);
            var remainingWidth = width - valueFieldSize.x - _list.margin;
            var fieldWidth = (remainingWidth - _list.margin) / 2;

            _list.AddColumn(fieldWidth, "action.blendshape.header.object", (rect, elem) =>
            {
                var targetProp = elem.FindPropertyRelative(nameof(ActionBlendshape.BlendshapeSpec.target));
                EditorGUI.PropertyField(rect, targetProp, GUIContent.none);
            });

            _list.AddColumn(fieldWidth, "action.blendshape.header.blendshape", RenderBlendshapeColumn);

            _list.AddColumn(valueFieldSize.x, "action.blendshape.header.value", (rect, elem) =>
            {
                var valueProp = elem.FindPropertyRelative(nameof(ActionBlendshape.BlendshapeSpec.value));
                EditorGUI.PropertyField(rect, valueProp, GUIContent.none);
            });
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            Rect rect = GUILayoutUtility.GetRect(
                10f,
                _list.headerHeight + _list.elementHeight * Math.Max(1, _list.serializedProperty.arraySize) +
                _list.footerHeight,
                GUILayout.ExpandWidth(true)
            );

            _list.DoList(rect);

            EditorGUILayout.Space(8);

            Localization.ShowLanguageUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}