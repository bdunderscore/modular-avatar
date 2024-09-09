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
        
        private readonly FloatField _defaultValueField;
        private readonly Toggle _hasExplicitDefaultSetField;
        private readonly TextField _notSyncedField;
        private readonly TextField _intField;
        private readonly TextField _floatField;
        private readonly DropdownField _boolField;

        public DefaultValueField()
        {
            // Hidden binding elements
            _defaultValueField = new FloatField();
            _defaultValueField.style.display = DisplayStyle.None;
            _hasExplicitDefaultSetField = new Toggle();
            _hasExplicitDefaultSetField.style.display = DisplayStyle.None;

            _defaultValueField.RegisterValueChangedCallback(
                evt => UpdateVisibleField(evt.newValue, _hasExplicitDefaultSetField.value));
            _defaultValueField.bindingPath = nameof(ParameterConfig.defaultValue);
            
            _hasExplicitDefaultSetField.RegisterValueChangedCallback(
                evt => UpdateVisibleField(_defaultValueField.value, evt.newValue));
            _hasExplicitDefaultSetField.bindingPath = nameof(ParameterConfig.hasExplicitDefaultValue);

            // Visible elements for input
            _notSyncedField = new TextField();
            _notSyncedField.name = "default-value-not-synced";
            _notSyncedField.isDelayed = true;
            _intField = new TextField();
            _intField.name = "default-value-int";
            _intField.isDelayed = true;
            _floatField = new TextField();
            _floatField.name = "default-value-float";
            _floatField.isDelayed = true;
            _boolField = new DropdownField();
            _boolField.name = "default-value-bool";
            _boolField.choices.Add(V_None);
            _boolField.choices.Add(V_True);
            _boolField.choices.Add(V_False);

            void NumberChangedCallback(ChangeEvent<string> evt)
            {
                if (string.IsNullOrWhiteSpace(evt.newValue))
                {
                    _defaultValueField.value = 0;
                    _hasExplicitDefaultSetField.value = false;
                }
                else if (float.TryParse(evt.newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    _defaultValueField.value = value;
                    _hasExplicitDefaultSetField.value = true;
                }
                else
                {
                    UpdateVisibleField(_defaultValueField.value, _hasExplicitDefaultSetField.value);
                }
            }

            _notSyncedField.RegisterValueChangedCallback(NumberChangedCallback);

            _intField.RegisterValueChangedCallback(NumberChangedCallback);

            _floatField.RegisterValueChangedCallback(NumberChangedCallback);

            _boolField.RegisterValueChangedCallback(evt =>
            {
                _defaultValueField.value = evt.newValue == V_True ? 1 : 0;
                _hasExplicitDefaultSetField.value = evt.newValue != V_None;
            });
            
            Add(_defaultValueField);
            Add(_hasExplicitDefaultSetField);
            Add(_notSyncedField);
            Add(_intField);
            Add(_floatField);
            Add(_boolField);
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

            _notSyncedField.SetValueWithoutNotify(hasExplicitValue
                ? value.ToString(CultureInfo.InvariantCulture)
                : string.Empty);

            _intField.SetValueWithoutNotify(hasExplicitValue
                ? Mathf.FloorToInt(Mathf.Clamp(value, 0, 255)).ToString(CultureInfo.InvariantCulture)
                : string.Empty);

            _floatField.SetValueWithoutNotify(hasExplicitValue
                ? Mathf.Clamp(value, -1, 1).ToString(CultureInfo.InvariantCulture)
                : string.Empty);

            _boolField.SetValueWithoutNotify(hasExplicitValue
                ? value != 0 ? V_True : V_False
                : V_None);
        }
    }
}