using System.Globalization;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class DefaultValueField : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<DefaultValueField, UxmlTraits>
        {
        }

        private readonly TextField _visibleField;
        private readonly FloatField _defaultValueField;
        private readonly Toggle _hasExplicitDefaultSetField;

        public DefaultValueField()
        {
            // Hidden binding elements
            _defaultValueField = new FloatField();
            _hasExplicitDefaultSetField = new Toggle();

            _defaultValueField.RegisterValueChangedCallback(
                evt => UpdateVisibleField(evt.newValue, _hasExplicitDefaultSetField.value));
            _defaultValueField.bindingPath = nameof(ParameterConfig.defaultValue);
            
            _hasExplicitDefaultSetField.RegisterValueChangedCallback(
                evt => UpdateVisibleField(_defaultValueField.value, evt.newValue));
            _hasExplicitDefaultSetField.bindingPath = nameof(ParameterConfig.hasExplicitDefaultValue);

            _visibleField = new TextField();
            _visibleField.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrWhiteSpace(evt.newValue))
                {
                    _hasExplicitDefaultSetField.value = false;
                    _defaultValueField.value = 0;
                }
                else
                {
                    _hasExplicitDefaultSetField.value = true;
                    _defaultValueField.value = float.Parse(evt.newValue, CultureInfo.InvariantCulture);
                }
            });
            
            _defaultValueField.style.width = 0;
            _defaultValueField.SetEnabled(false);
            _hasExplicitDefaultSetField.style.width = 0;
            _hasExplicitDefaultSetField.SetEnabled(false);
            
            style.flexDirection = FlexDirection.Row;
            
            Add(_visibleField);
            Add(_defaultValueField);
            Add(_hasExplicitDefaultSetField);
        }

        public void ManualBindProperty(SerializedProperty property)
        {
            _defaultValueField.BindProperty(property);
            _hasExplicitDefaultSetField.BindProperty(property);
        }
        
        private void UpdateVisibleField(float value, bool hasExplicitValue)
        {
            if (Mathf.Abs(value) > 0.0000001)
            {
                hasExplicitValue = true;
            }
            
            var str = hasExplicitValue ? value.ToString(CultureInfo.InvariantCulture) : "";
            _visibleField.SetValueWithoutNotify(str);
        }
    }
}