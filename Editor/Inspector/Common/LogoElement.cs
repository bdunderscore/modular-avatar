#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    public class LogoElement : VisualElement
    {
        private const string LISTENER_REGISTERED = "ma--logo-listener-registered";
        private static WeakHashSet<LogoElement> _activeLogos = new WeakHashSet<LogoElement>();

        private static Dictionary<VisualElement, LogoElement> _logoDisplayNode
            = null;

        private VisualElement _inner;

        public new class UxmlFactory : UxmlFactory<LogoElement, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }
        
        private static void RegisterNode(LogoElement target)
        {
            if (_logoDisplayNode == null)
            {
                _logoDisplayNode = new Dictionary<VisualElement, LogoElement>();
                EditorApplication.delayCall += () => { _logoDisplayNode = null; };
            }

            // [editor list] -> EditorElement (private) -> InspectorElement -> MAVisualElement -> Logo
            VisualElement container = target;
            while (container.parent != null && !(container is InspectorElement))
            {
                container = container.parent;
            }

            container = container.parent ?? container; // EditorElement
            container = container.parent ?? container; // editor list

            if (container.ClassListContains(LISTENER_REGISTERED)) return;
            container.RegisterCallback<GeometryChangedEvent>(geom => { UpdateLogoDisplayNode(container); });
        }

        private static void UpdateLogoDisplayNode(VisualElement root)
        {
            // Now walk down to find the LogoElements. We only walk one level past an InspectorElement (and once into
            // its child MAVisualElement) to avoid descending too deep into madness.
            List<LogoElement> elements = new List<LogoElement>();

            WalkTree(root);

            var target = elements.FirstOrDefault(e => e.resolvedStyle.visibility == Visibility.Visible);
            foreach (var elem in elements)
            {
                elem.LogoShown = (elem == target);
            }

            void WalkTree(VisualElement visualElement)
            {
                if (visualElement.resolvedStyle.visibility == Visibility.Hidden ||
                    visualElement.resolvedStyle.height < 0.5) return;

                var isInspector = visualElement.GetType() == typeof(InspectorElement);

                foreach (var child in visualElement.Children())
                {
                    if (child is MAVisualElement maChild)
                    {
                        foreach (var node in child.Children())
                        {
                            if (node is LogoElement logo)
                            {
                                elements.Add(logo);
                            }
                        }

                        return;
                    }
                    else if (!isInspector)
                    {
                        WalkTree(child);
                    }
                }
            }
        }

        public LogoElement()
        {
            _inner = new LogoImage();
            _inner.style.display = DisplayStyle.None;
            this.Add(_inner);

            RegisterCallback<GeometryChangedEvent>(OnGeomChanged);
        }

        private void OnGeomChanged(GeometryChangedEvent evt)
        {
            // We should be in the visual tree now
            if (parent == null) return;

            RegisterNode(this);

            UnregisterCallback<GeometryChangedEvent>(OnGeomChanged);
        }

        private bool _logoShown;

        private bool LogoShown
        {
            get => _logoShown;
            set
            {
                if (value == _logoShown) return;
                _logoShown = value;

                _inner.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}