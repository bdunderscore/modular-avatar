using System;
using System.Globalization;
using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    public class LanguageSwitcherElement : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<LanguageSwitcherElement, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }

        public LanguageSwitcherElement()
        {
            _ = Localization.L;
            var languageCodes = LanguagePrefs.RegisteredLanguages
                .Where(lang => lang.Contains("-") ||
                               LanguagePrefs.RegisteredLanguages.All(other => !other.StartsWith(lang + "-")))
                .ToList();
            var displayNames = languageCodes.Select(GetDisplayName).ToList();
            var selectedIndex = languageCodes.IndexOf(LanguagePrefs.Language);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var field = new DropdownField("Editor Language", displayNames, selectedIndex);
            field.RegisterValueChangedCallback(change =>
            {
                var newIndex = displayNames.IndexOf(change.newValue);
                if (newIndex >= 0)
                {
                    LanguagePrefs.Language = languageCodes[newIndex];
                }
            });
            Add(field);
        }

        private static string GetDisplayName(string languageCode)
        {
            try
            {
                return CultureInfo.CreateSpecificCulture(languageCode).NativeName;
            }
            catch (Exception)
            {
                return languageCode;
            }
        }
    }
}
