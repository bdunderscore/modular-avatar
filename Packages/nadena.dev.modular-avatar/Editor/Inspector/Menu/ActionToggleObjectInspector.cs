using System;
using System.Collections.Generic;
using System.Linq;
using BestHTTP;
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
        private ColumnReorderableList _list;
        private SerializedProperty _listProp;

        private void OnEnable()
        {
            _listProp = serializedObject.FindProperty(nameof(ActionToggleObject.Objects));
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

        private void BuildColumns(float width)
        {
            var t = EditorStyles.toggle;
            var toggleSize = t.CalcSize(GUIContent.none);

            var checkHeaderContent = G("action.toggle_object.header.show");
            var rectHeaderSize = EditorStyles.label.CalcSize(checkHeaderContent);

            width = _list.AddColumn(rectHeaderSize.x, "action.toggle_object.header.show", (rect, prop) =>
            {
                rect = CenterElement(rect, toggleSize);

                var activeProp = prop.FindPropertyRelative(nameof(ActionToggleObject.ObjectEntry.Active));
                EditorGUI.PropertyField(rect, activeProp, GUIContent.none);
            });

            _list.AddColumn(width, "action.toggle_object.header.object", ((rect, elem) =>
            {
                var targetProp = elem.FindPropertyRelative(nameof(ActionToggleObject.ObjectEntry.target));
                EditorGUI.PropertyField(rect, targetProp, GUIContent.none);
            }));
        }

        private static Rect CenterElement(Rect rect, Vector2 toggleSize)
        {
            float adjust = rect.height - toggleSize.y;
            rect.yMin += adjust / 2;
            rect.yMax -= adjust / 2;

            adjust = rect.width - toggleSize.x;
            rect.xMin += adjust / 2;
            rect.xMax -= adjust / 2;

            return rect;
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
                                currentObjects.Add(obj.target.Get(targetObj));
                            }
                        }

                        var objects = targetObj.Objects.ToList();

                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go && !currentObjects.Contains(go))
                            {
                                objects.Add(new ActionToggleObject.ObjectEntry()
                                {
                                    target = new AvatarObjectReference()
                                    {
                                        referencePath = RuntimeUtil.AvatarRootPath(go)
                                    },
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