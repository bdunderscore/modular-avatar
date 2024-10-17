using System.Globalization;
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
        private readonly Toggle _hasExplicitDefaultValueField;
        private readonly TextField _numberField;
        private readonly DropdownField _boolField;

        private ParameterSyncType _syncType;

        public DefaultValueField()
        {
            // Hidden binding elements
            _defaultValueField = new FloatField();
            _defaultValueField.style.display = DisplayStyle.None;
            _defaultValueField.bindingPath = nameof(ParameterConfig.defaultValue);
            _defaultValueField.RegisterValueChangedCallback(evt => UpdateVisibleField(evt.newValue, _hasExplicitDefaultValueField.value));
            _hasExplicitDefaultValueField = new Toggle();
            _hasExplicitDefaultValueField.style.display = DisplayStyle.None;
            _hasExplicitDefaultValueField.bindingPath = nameof(ParameterConfig.hasExplicitDefaultValue);
            _hasExplicitDefaultValueField.RegisterValueChangedCallback(evt => UpdateVisibleField(_defaultValueField.value, evt.newValue));

            // Visible elements for input
            _numberField = new TextField();
            _numberField.isDelayed = true;
            _numberField.RegisterValueChangedCallback(evt => OnUpdateNumberValue(evt.newValue));
            _boolField = new DropdownField();
            _boolField.choices.Add(V_None);
            _boolField.choices.Add(V_True);
            _boolField.choices.Add(V_False);
            _boolField.RegisterValueChangedCallback(evt => OnUpdateBoolValue(evt.newValue));

            Add(_defaultValueField);
            Add(_hasExplicitDefaultValueField);
            Add(_numberField);
            Add(_boolField);
        }

        public void OnUpdateSyncType(ParameterSyncType syncType)
        {
            _syncType = syncType;

            if (syncType != ParameterSyncType.Bool)
            {
                _numberField.style.display = DisplayStyle.Flex;
                _boolField.style.display = DisplayStyle.None;
                OnUpdateNumberValue(_numberField.value);
            }
            else
            {
                _numberField.style.display = DisplayStyle.None;
                _boolField.style.display = DisplayStyle.Flex;
                OnUpdateBoolValue(_boolField.value);
            }
        }

        private void OnUpdateNumberValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _defaultValueField.value = 0;
                _hasExplicitDefaultValueField.value = false;
            }
            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && !float.IsNaN(parsed)
                && !float.IsInfinity(parsed))
            {
                _defaultValueField.value = _syncType switch
                {
                    ParameterSyncType.Int => Mathf.FloorToInt(Mathf.Clamp(parsed, 0, 255)),
                    ParameterSyncType.Float => Mathf.Clamp(parsed, -1, 1),
                    ParameterSyncType.Bool => parsed != 0 ? 1 : 0,
                    _ => parsed,
                };
                _hasExplicitDefaultValueField.value = true;
            }

            UpdateVisibleField(_defaultValueField.value, _hasExplicitDefaultValueField.value);
        }

        private void OnUpdateBoolValue(string value)
        {
            _defaultValueField.value = value == V_True ? 1 : 0;
            _hasExplicitDefaultValueField.value = value != V_None;

            UpdateVisibleField(_defaultValueField.value, _hasExplicitDefaultValueField.value);
        }

        private void UpdateVisibleField(float value, bool hasExplicitValue)
        {
            if (hasExplicitValue || Mathf.Abs(value) > 0.0000001)
            {
                _numberField.SetValueWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
                _boolField.SetValueWithoutNotify(value != 0 ? V_True : V_False);
            }
            else
            {
                _numberField.SetValueWithoutNotify(string.Empty);
                _boolField.SetValueWithoutNotify(V_None);
            }
        }
    }
}
