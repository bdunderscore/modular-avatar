#region

using UnityEditor;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    public partial class LogoImage : VisualElement
    {
        VisualElement _inner;

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<LogoImage, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }
#endif

        public LogoImage()
        {
            _inner = new VisualElement();

            _inner.style.flexDirection = FlexDirection.Row;
            _inner.style.alignItems = Align.Center;
            _inner.style.justifyContent = Justify.Center;

            var image = new Image();
            image.image = LogoDisplay.LOGO_ASSET;

            SetImageSize(image);

            _inner.Add(image);
            Add(_inner);
        }

        private static void SetImageSize(Image image, int maxTries = 10)
        {
            var targetHeight = LogoDisplay.TARGET_HEIGHT;

            if (targetHeight == 0)
            {
                if (maxTries <= 0) return;
                EditorApplication.delayCall += () => SetImageSize(image, maxTries - 1);
                targetHeight = 45;
            }

            image.style.width = new Length(LogoDisplay.ImageWidth(targetHeight), LengthUnit.Pixel);
            image.style.height = new Length(targetHeight, LengthUnit.Pixel);
        }
    }
}
