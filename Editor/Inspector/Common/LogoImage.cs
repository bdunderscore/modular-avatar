#region

using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    public class LogoImage : VisualElement
    {
        VisualElement _inner;

        public new class UxmlFactory : UxmlFactory<LogoImage, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }

        public LogoImage()
        {
            _inner = new VisualElement();

            _inner.style.flexDirection = FlexDirection.Row;
            _inner.style.alignItems = Align.Center;
            _inner.style.justifyContent = Justify.Center;

            var image = new Image();
            image.image = LogoDisplay.LOGO_ASSET;
            image.style.width = new Length(LogoDisplay.ImageWidth(LogoDisplay.TARGET_HEIGHT), LengthUnit.Pixel);
            image.style.height = new Length(LogoDisplay.TARGET_HEIGHT, LengthUnit.Pixel);

            _inner.Add(image);
            Add(_inner);
        }
    }
}