#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class RestrictedEnumDropdown
    {
        public static void Bind<TEnum>(
            DropdownField dropdown,
            SerializedProperty property,
            IReadOnlyList<TEnum> choices,
            string localizationPrefix
        ) where TEnum : struct, Enum
        {
            if (choices.Count == 0)
            {
                throw new ArgumentException("At least one enum choice must be supported.", nameof(choices));
            }

            var enumToIndex = BuildEnumIndexMap<TEnum>();
            var rawChoices = choices
                .Select(choice => choice.ToString())
                .ToList();
            var rawChoiceToEnumIndex = choices.ToDictionary(
                choice => choice.ToString(),
                choice => enumToIndex[choice]
            );
            var supportedEnumIndices = rawChoiceToEnumIndex.Values.ToArray();

            dropdown.choices = rawChoices;
            dropdown.formatListItemCallback = value => LocalizedChoice(localizationPrefix, value);
            dropdown.formatSelectedValueCallback = value => LocalizedChoice(localizationPrefix, value);

            void RefreshValue()
            {
                var selectedEnumIndex = NormalizeToSupportedEnumIndex(property, supportedEnumIndices);
                var selectedRawValue = rawChoiceToEnumIndex
                    .First(pair => pair.Value == selectedEnumIndex)
                    .Key;
                dropdown.SetValueWithoutNotify(selectedRawValue);
                dropdown.MarkDirtyRepaint();
            }

            RefreshValue();
            LanguagePrefs.RegisterLanguageChangeCallback(dropdown, _ => RefreshValue());

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (!rawChoiceToEnumIndex.TryGetValue(evt.newValue, out var enumIndex)) return;

                property.serializedObject.Update();
                property.enumValueIndex = enumIndex;
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        private static Dictionary<TEnum, int> BuildEnumIndexMap<TEnum>() where TEnum : struct, Enum
        {
            var rawValues = Enum.GetValues(typeof(TEnum));
            var result = new Dictionary<TEnum, int>();

            for (var i = 0; i < rawValues.Length; i++)
            {
                result[(TEnum)rawValues.GetValue(i)] = i;
            }

            return result;
        }

        private static int NormalizeToSupportedEnumIndex(
            SerializedProperty property,
            IReadOnlyList<int> supportedEnumIndices
        )
        {
            if (supportedEnumIndices.Contains(property.enumValueIndex))
            {
                return property.enumValueIndex;
            }

            property.serializedObject.Update();
            property.enumValueIndex = supportedEnumIndices[0];
            property.serializedObject.ApplyModifiedProperties();
            return property.enumValueIndex;
        }

        private static string LocalizedChoice(string localizationPrefix, string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : Localization.S(localizationPrefix + "." + value);
        }
    }
}
