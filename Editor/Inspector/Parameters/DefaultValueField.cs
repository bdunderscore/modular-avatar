using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    internal partial class DefaultValueField : VisualElement
    {
#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<DefaultValueField, UxmlTraits>
        {
        }
#endif

        private const string V_None = "　";
        private const string V_True = "ON";
        private const string V_False = "OFF";

        private readonly FloatField _defaultValueField;
        private readonly Toggle _hasExplicitDefaultValueField;
        private readonly TextField _numberField;
        private readonly DropdownField _boolField;

        private ParameterSyncType _syncType;
        private int _bitWidth;
        private ParameterRepresentation _representation;
        private bool _hasInitialBinding;

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
                OnUpdateNumberValue(_numberField.value, true);
            }
            else
            {
                _numberField.style.display = DisplayStyle.None;
                _boolField.style.display = DisplayStyle.Flex;
                OnUpdateBoolValue(_boolField.value, true);
            }
        }

        public void OnUpdateNumericFormat(int bitWidth, ParameterRepresentation representation)
        {
            _bitWidth = bitWidth;
            _representation = representation;
        }

        private (float, float) GetValueRange(bool implicitUpdate)
        {
            var maxBitWidth = ParameterConfig.MAX_BIT_WIDTH;
            if (implicitUpdate)
            {
                return _syncType switch
                {
                    ParameterSyncType.Int => (-(1 << (maxBitWidth - 1)), (1 << maxBitWidth) - 1),
                    ParameterSyncType.Float => (-1, 1),
                    _ => (float.NegativeInfinity, float.PositiveInfinity),
                };
            }

            var bitWidth = ParameterConfig.ResolveBitWidth(_bitWidth);
            var signed = ParameterConfig.ResolveRepresentation(_representation, _syncType) ==
                         ParameterRepresentation.Signed;

            return _syncType switch
            {
                ParameterSyncType.Int => signed
                    ? (-(1 << (bitWidth - 1)), (1 << (bitWidth - 1)) - 1)
                    : (0, (1 << bitWidth) - 1),
                ParameterSyncType.Float => (signed ? -1 : 0, 1),
                _ => (float.NegativeInfinity, float.PositiveInfinity),
            };
        }

        private void OnUpdateNumberValue(string value, bool implicitUpdate = false)
        {
            // Upon initial creation, sometimes the OnUpdateSyncType fires before we receive the initial value event.
            // In this case, suppress the update to avoid losing data.
            if (implicitUpdate && !_hasInitialBinding) return;

            var theValue = _defaultValueField.value;
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!implicitUpdate)
                {
                    _defaultValueField.value = 0;
                }

                theValue = _defaultValueField.value;

                _hasExplicitDefaultValueField.value = false;
            }
            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && !float.IsNaN(parsed)
                && !float.IsInfinity(parsed))
            {
                var (min, max) = GetValueRange(implicitUpdate);
                theValue = _defaultValueField.value = _syncType switch
                {
                    ParameterSyncType.Int => Mathf.FloorToInt(Mathf.Clamp(parsed, min, max)),
                    ParameterSyncType.Float => Mathf.Clamp(parsed, min, max),
                    ParameterSyncType.Bool => parsed != 0 ? 1 : 0,
                    _ => parsed,
                };
                _hasExplicitDefaultValueField.value = true;
            }

            UpdateVisibleField(theValue, _hasExplicitDefaultValueField.value);
        }

        private void OnUpdateBoolValue(string value, bool implicitUpdate = false)
        {
            // Upon initial creation, sometimes the OnUpdateSyncType fires before we receive the initial value event.
            // In this case, suppress the update to avoid losing data.
            if (implicitUpdate && !_hasInitialBinding) return;

            _defaultValueField.value = value == V_True ? 1 : 0;
            _hasExplicitDefaultValueField.value = value != V_None;

            UpdateVisibleField(_defaultValueField.value, _hasExplicitDefaultValueField.value);
        }

        private void UpdateVisibleField(float value, bool hasExplicitValue)
        {
            _hasInitialBinding = true;

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
