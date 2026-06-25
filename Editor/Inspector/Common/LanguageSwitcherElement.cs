using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    public partial class LanguageSwitcherElement : VisualElement
    {
#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<LanguageSwitcherElement, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }
#endif

        public LanguageSwitcherElement()
        {
            // DropdownField is not supported in 2019...
            var imgui = new IMGUIContainer(Localization.ShowLanguageUI);
            Add(imgui);
        }
    }
}
