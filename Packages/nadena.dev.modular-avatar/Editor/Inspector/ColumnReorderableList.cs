using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    public class ColumnReorderableList : ReorderableList
    {
        public ColumnReorderableList(IList elements, Type elementType) : base(elements, elementType)
        {
            throw new NotSupportedException();
        }

        public ColumnReorderableList(IList elements, Type elementType, bool draggable, bool displayHeader,
            bool displayAddButton, bool displayRemoveButton) : base(elements, elementType, draggable, displayHeader,
            displayAddButton, displayRemoveButton)
        {
            throw new NotSupportedException();
        }

        public ColumnReorderableList(SerializedObject serializedObject, SerializedProperty elements) : base(
            serializedObject, elements)
        {
            Init();
            _elements = elements;
        }

        public ColumnReorderableList(SerializedObject serializedObject, SerializedProperty elements, bool draggable,
            bool displayHeader, bool displayAddButton, bool displayRemoveButton) : base(serializedObject, elements,
            draggable, displayHeader, displayAddButton, displayRemoveButton)
        {
            Init();
            _elements = elements;
        }


        private struct Column
        {
            public string headerKey;
            public Action<Rect, SerializedProperty> drawField;
            public float offsetX, width;
        }

        private SerializedProperty _elements;
        private float _elementWidth, _remainingWidth;
        private readonly List<Column> _columns = new List<Column>();
        private float _columnPos;
        public float margin = 4;

        public Action<float> OnGenerateColumns = (_) => { };
        public Action Repaint;

        public float AddColumn(float width, string headerKey, Action<Rect, SerializedProperty> drawField)
        {
            _columns.Add(new Column
            {
                headerKey = headerKey,
                drawField = drawField,
                offsetX = _columnPos,
                width = width
            });

            width += margin;

            _columnPos += width;
            _remainingWidth -= width;

            return _remainingWidth;
        }

        void Init()
        {
            drawHeaderCallback = DrawListHeader;
            drawElementCallback = DrawElement;
        }

        private void DrawElement(Rect rect, int i, bool isactive, bool isfocused)
        {
            if (Mathf.Abs(_elementWidth - rect.width) > 0.01f && rect.width > 1)
            {
                _elementWidth = rect.width;
                _columns.Clear();
                Repaint();
            }

            var element = _elements.GetArrayElementAtIndex(i);

            using (new ZeroIndentScope())
            {
                foreach (var column in _columns)
                {
                    var elemRect = rect;
                    elemRect.xMin += column.offsetX;
                    elemRect.width = column.width;

                    column.drawField(elemRect, element);
                }
            }
        }

        private void DrawListHeader(Rect rect)
        {
            if (_elementWidth > 1 && _elementWidth < rect.width)
            {
                rect.x += rect.width - _elementWidth;
                rect.width = _elementWidth;
            }

            if (_columns.Count == 0)
            {
                if (_elementWidth < 1.0f)
                {
                    if (rect.width < 1.0f)
                    {
                        return;
                    }

                    _elementWidth = rect.width - 10f;
                    Repaint();
                }

                _columnPos = 0;
                _remainingWidth = _elementWidth;
                OnGenerateColumns(_elementWidth);
            }

            foreach (var column in _columns)
            {
                var elemRect = rect;
                elemRect.xMin += column.offsetX;
                elemRect.width = column.width;

                EditorGUI.LabelField(elemRect, G(column.headerKey));
            }
        }
    }
}