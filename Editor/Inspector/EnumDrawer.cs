using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal abstract class EnumDrawer<T> : PropertyDrawer where T : Enum
    {
        protected abstract string localizationPrefix { get; }

        protected virtual Array enumValues => Enum.GetValues(typeof(T));

        private Dictionary<int, int> _enumToContentIndex;
        private Dictionary<int, int> _contentIndexToEnum;

        private Dictionary<T, int> _objectToEnumIndex;

        private GUIContent[] _content;
        private string _cachedLanguage;

        internal EnumDrawer()
        {
            var rawValues = Enum.GetValues(typeof(T));
            _objectToEnumIndex = new Dictionary<T, int>();

            int i = 0;
            foreach (var val in rawValues)
            {
                _objectToEnumIndex.Add((T) val, i);
                i++;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (_content == null || _cachedLanguage != Localization.GetSelectedLocalization())
            {
                var values = enumValues;
                _content = new GUIContent[values.Length];

                _enumToContentIndex = new Dictionary<int, int>();
                _contentIndexToEnum = new Dictionary<int, int>();

                int i = 0;
                foreach (var val in values)
                {
                    _enumToContentIndex.Add(_objectToEnumIndex[(T) val], i);
                    _contentIndexToEnum.Add(i, _objectToEnumIndex[(T) val]);
                    _content[i++] = Localization.G(localizationPrefix + "." + val);
                }

                _cachedLanguage = Localization.GetSelectedLocalization();
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            var currentIndex = -1;
            if (_enumToContentIndex.ContainsKey(property.enumValueIndex))
            {
                currentIndex = _enumToContentIndex[property.enumValueIndex];
            }

            var value = EditorGUI.Popup(position, label, currentIndex, _content);

            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = _contentIndexToEnum[value];
            }

            EditorGUI.EndProperty();
        }
    }
}