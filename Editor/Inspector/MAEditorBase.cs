using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    // This class performs common setup for Modular Avatar editors, including ensuring that only one instance of the\
    // logo is rendered per container.
    public abstract class MAEditorBase : UnityEditor.Editor
    {
        private MAVisualElement _visualElement;
        private bool _suppressOnceDefaultMargins;

        protected virtual VisualElement CreateInnerInspectorGUI()
        {
            return null;
        }

        private void RebuildUI()
        {
            CreateInspectorGUI();
        }

        public sealed override VisualElement CreateInspectorGUI()
        {
            if (_visualElement == null)
            {
                _visualElement = new MAVisualElement();
                Localization.OnLangChange += RebuildUI;
            }
            else
            {
                _visualElement.Clear();
            }

            _visualElement.Add(new LogoElement());

            // CreateInspectorElementFromEditor does a bunch of extra setup that makes our inspector look a little bit
            // nicer. In particular, the label column won't auto-size if we just use IMGUIElement, for some reason 

            var inner = CreateInnerInspectorGUI();

            bool innerIsImgui = (inner == null);
            if (innerIsImgui)
            {
                var throwaway = new InspectorElement();
                inner = typeof(InspectorElement).GetMethod("CreateIMGUIInspectorFromEditor",
                                BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(throwaway, new object[] {serializedObject, this, false}) as VisualElement
                        ?? typeof(InspectorElement).GetMethod("CreateInspectorElementUsingIMGUI",
                                BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(throwaway, new object[] { this }) as VisualElement;
            }

            _visualElement.Add(inner);

            _suppressOnceDefaultMargins = innerIsImgui;
            return _visualElement;
        }

        public override bool UseDefaultMargins()
        {
            var useDefaults = !_suppressOnceDefaultMargins;
            _suppressOnceDefaultMargins = false;
            return useDefaults;
        }

        public sealed override void OnInspectorGUI()
        {
            InspectorCommon.DisplayOutOfAvatarWarning(targets);

            OnInnerInspectorGUI();
        }

        protected abstract void OnInnerInspectorGUI();

        protected virtual void OnDestroy()
        {
            Localization.OnLangChange -= RebuildUI;
        }
    }

    internal class MAVisualElement : VisualElement
    {
    }
}