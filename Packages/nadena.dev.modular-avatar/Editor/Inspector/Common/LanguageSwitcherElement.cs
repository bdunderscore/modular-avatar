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
            // DropdownField is not supported in 2019...
            var imgui = new IMGUIContainer(Localization.ShowLanguageUI);
            Add(imgui);
        }
    }
}