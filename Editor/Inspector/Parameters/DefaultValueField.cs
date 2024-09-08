using System.Globalization;
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

        private const string V_None = "　";
        private const string V_True = "ON";
        private const string V_False = "OFF";
        
        private readonly TextField _visibleField;
        private readonly FloatField _defaultValueField;
        private readonly DropdownField _boolField;
        private readonly Toggle _hasExplicitDefaultSetField;

        public DefaultValueField()
        {
            // Hidden binding elements
            _defaultValueField = new FloatField();
            _hasExplicitDefaultSetField = new Toggle();
            _boolField = new DropdownField();

            _boolField.choices.Add(V_None);
            _boolField.choices.Add(V_True);
            _boolField.choices.Add(V_False);

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

            _boolField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == V_True)
                    _defaultValueField.value = 1;
                else
                    _defaultValueField.value = 0;

                _hasExplicitDefaultSetField.value = evt.newValue != V_None;
            });
            
            
            style.flexDirection = FlexDirection.Row;
            
            Add(_visibleField);
            Add(_boolField);
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

            string boolStr;
            if (!hasExplicitValue)
                boolStr = V_None;
            else if (value > 0.5)
                boolStr = V_True;
            else
                boolStr = V_False;

            _boolField.SetValueWithoutNotify(boolStr);
        }
    }
}