using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    // This class performs common setup for Modular Avatar editors, including ensuring that only one instance of the\
    // logo is rendered per container.
    internal abstract class MAEditorBase : Editor
    {
        private static Dictionary<VisualElement, MAVisualElement>
            _logoDisplayNode = new Dictionary<VisualElement, MAVisualElement>();

        private static void Cleanup()
        {
            _logoDisplayNode.Clear();
            EditorApplication.update -= Cleanup;
        }

        private static MAVisualElement GetCachedLogoDisplayNode(VisualElement start)
        {
            while (start?.parent != null && start.GetType() != typeof(InspectorElement))
            {
                start = start.parent;
            }

            // Next one up is an EditorElement, followed by the container of all Editors
            var container = start?.parent?.parent;

            if (container == null) return null;

            if (_logoDisplayNode.TryGetValue(container, out var elem)) return elem;

            var node = FindLogoDisplayNode(container);
            if (node != null) _logoDisplayNode[container] = node;
            EditorApplication.update += Cleanup;
            return node;
        }

        private static MAVisualElement FindLogoDisplayNode(VisualElement container)
        {
            // Now walk down to find the MAVisualElements. We only walk one level past an InspectorElement to avoid
            // descending too deep into madness.
            List<MAVisualElement> elements = new List<MAVisualElement>();

            WalkTree(container);

            return elements.FirstOrDefault(e => e.resolvedStyle.visibility == Visibility.Visible);

            void WalkTree(VisualElement visualElement)
            {
                if (visualElement.resolvedStyle.visibility == Visibility.Hidden ||
                    visualElement.resolvedStyle.height < 0.5) return;

                var isInspector = visualElement.GetType() == typeof(InspectorElement);

                foreach (var child in visualElement.Children())
                {
                    if (child is MAVisualElement maChild)
                    {
                        elements.Add(maChild);
                        return;
                    }
                    else if (!isInspector)
                    {
                        WalkTree(child);
                    }
                }
            }
        }

        private class MAVisualElement : VisualElement
        {
        }

        private MAVisualElement _visualElement;
        private bool _suppressOnceDefaultMargins;

        protected virtual VisualElement CreateInnerInspectorGUI()
        {
            return null;
        }

        public sealed override VisualElement CreateInspectorGUI()
        {
            // CreateInspectorElementFromEditor does a bunch of extra setup that makes our inspector look a little bit
            // nicer. In particular, the label column won't auto-size if we just use IMGUIElement, for some reason 

            var inner = CreateInnerInspectorGUI();

            bool innerIsImgui = (inner == null);
            if (innerIsImgui)
            {
                var throwaway = new InspectorElement();
                MethodInfo m = typeof(InspectorElement).GetMethod("CreateIMGUIInspectorFromEditor",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                inner = m.Invoke(throwaway, new object[] {serializedObject, this, false}) as VisualElement;
            }

            _visualElement = new MAVisualElement();
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
            if (GetCachedLogoDisplayNode(_visualElement) == _visualElement)
            {
                LogoDisplay.DisplayLogo();
            }

            InspectorCommon.DisplayOutOfAvatarWarning(targets);
            if (!ComponentAllowlistPatch.PATCH_OK) InspectorCommon.DisplayVRCSDKVersionWarning();

            OnInnerInspectorGUI();
        }

        protected abstract void OnInnerInspectorGUI();
    }
}