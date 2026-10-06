using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    public class LanguageSwitcherElement : VisualElement
    {
        private readonly LanguageSwitcher _inner = new();
        
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
            Add(_inner);
        }
    }
}
