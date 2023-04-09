using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.Collections;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ActionToggleObject))]
    internal class ActionToggleObjectInspector : MAEditorBase
    {
        private ReorderableList _list;
        private SerializedProperty _listProp;

        private void OnEnable()
        {
            _listProp = serializedObject.FindProperty(nameof(ActionToggleObject.Objects));
            _list = new ReorderableList(
                serializedObject,
                _listProp,
                true,
                true,
                true,
                true
            );

            _list.drawHeaderCallback += DrawListHeader;
            _list.drawElementCallback += DrawElement;
            _list.onRemoveCallback += OnRemoveElement;
            _list.onAddCallback += OnAddElement;

            _list.elementHeight += 2;
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

        private float elementWidth;
        private GUIContent _rectHeader, _objectHeader;
        private Rect _checkRect, _objectRect;

        private void DrawListHeader(Rect rect)
        {
            if (elementWidth > 1 && elementWidth < rect.width)
            {
                rect.x += rect.width - elementWidth;
                rect.width = elementWidth;
            }

            var margin = 4;

            var t = EditorStyles.toggle;
            var toggleSize = t.CalcSize(GUIContent.none);
            _checkRect = new Rect(0, 0, toggleSize.x, toggleSize.y);

            _rectHeader = G("action.toggle_object.header.show");
            _objectHeader = G("action.toggle_object.header.object");

            var checkLabelRect = _checkRect;

            var rectHeaderSize = EditorStyles.label.CalcSize(_rectHeader);
            checkLabelRect.width = Mathf.Max(checkLabelRect.width, rectHeaderSize.x);
            _checkRect.x += (checkLabelRect.width - _checkRect.width) / 2;
            _checkRect.width = checkLabelRect.width;

            _checkRect.y += (rect.height - _checkRect.height);

            _objectRect = new Rect(checkLabelRect.xMax + margin, 0, rect.width - checkLabelRect.width - margin,
                _list.elementHeight);

            //EditorGUI.LabelField(rect, G("action.toggle_object.header"));
            var headerCheckRect = checkLabelRect;
            headerCheckRect.x += rect.x;
            headerCheckRect.height = rectHeaderSize.y;
            // why does *2 work well here???
            headerCheckRect.y += (rect.height - headerCheckRect.height) * 2;

            var headerObjectRect = _objectRect;
            headerObjectRect.x += rect.x;
            headerObjectRect.height = rectHeaderSize.y;
            headerObjectRect.y += (rect.height - headerObjectRect.height) * 2;

            EditorGUI.LabelField(headerCheckRect, _rectHeader);
            EditorGUI.LabelField(headerObjectRect, _objectHeader);
        }

        private void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            if (Mathf.Abs(elementWidth - rect.width) > 0.01f && rect.width > 1)
            {
                elementWidth = rect.width;
                Repaint();
            }

            var element = _listProp.GetArrayElementAtIndex(index);
            var activeProp = element.FindPropertyRelative(nameof(ActionToggleObject.ObjectEntry.Active));
            var targetProp = element.FindPropertyRelative(nameof(ActionToggleObject.ObjectEntry.target));

            var checkRect = _checkRect;
            checkRect.x += rect.x;
            checkRect.y += rect.y;

            var objectRect = _objectRect;
            objectRect.x += rect.x;
            objectRect.y += rect.y;
            objectRect.xMax = rect.xMax;
            objectRect.yMin += 1;
            objectRect.yMax -= 1;

            using (new ZeroIndentScope())
            {
                EditorGUI.PropertyField(checkRect, activeProp, GUIContent.none);
                EditorGUI.PropertyField(objectRect, targetProp, GUIContent.none);
            }
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

            if (rect.Contains(Event.current.mousePosition))
            {
                switch (Event.current.type)
                {
                    case EventType.DragUpdated:
                        if (!DragIsGameObject()) break;
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        Event.current.Use();
                        break;
                    case EventType.DragPerform:
                    {
                        if (!DragIsGameObject()) break;
                        var targetObj = (ActionToggleObject) target;

                        if (targetObj.Objects == null)
                        {
                            targetObj.Objects = new List<ActionToggleObject.ObjectEntry>();
                        }

                        HashSet<GameObject> currentObjects = new HashSet<GameObject>();
                        foreach (var obj in targetObj.Objects)
                        {
                            if (obj != null && obj.target != null)
                            {
                                currentObjects.Add(obj.target);
                            }
                        }

                        var objects = targetObj.Objects.ToList();

                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go && !currentObjects.Contains(go))
                            {
                                objects.Add(new ActionToggleObject.ObjectEntry()
                                {
                                    target = go,
                                    Active = go.activeSelf
                                });
                            }
                        }

                        Undo.RecordObject(targetObj, "Add objects");
                        targetObj.Objects = objects;
                        EditorUtility.SetDirty(targetObj);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(targetObj);
                        break;
                    }
                }
            }
        }

        private bool DragIsGameObject()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject) return true;
            }

            return false;
        }
    }
}