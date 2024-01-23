using System;
using System.Collections.Generic;
using System.Reflection;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class UXMLExtensions
    {
        private static Dictionary<Type, Action<VisualElement>> _localizers =
            new Dictionary<Type, Action<VisualElement>>();

        [Obsolete("Use UIElementLocalizer instead")]
        public static VisualElement Localize(this VisualTreeAsset asset)
        {
            var root = asset.CloneTree();

            WalkTree(root);

            return root;
        }

        private static void WalkTree(VisualElement elem)
        {
            var ty = elem.GetType();

            GetLocalizationOperation(ty)(elem);

            foreach (var child in elem.Children())
            {
                WalkTree(child);
            }
        }

        private static Action<VisualElement> GetLocalizationOperation(Type ty)
        {
            if (!_localizers.TryGetValue(ty, out var action))
            {
                PropertyInfo m_label;
                if (ty == typeof(Label))
                {
                    m_label = ty.GetProperty("text");
                }
                else
                {
                    m_label = ty.GetProperty("label");
                }

                if (m_label == null)
                {
                    action = _elem => { };
                }
                else
                {
                    action = elem =>
                    {
                        var cur_label = m_label.GetValue(elem) as string;
                        if (cur_label != null && cur_label.StartsWith("##"))
                        {
                            var key = cur_label.Substring(2);

                            var new_label = Localization.S(key);
                            var new_tooltip = Localization.S(key + ".tooltip");

                            m_label.SetValue(elem, new_label);
                            elem.tooltip = new_tooltip;
                        }
                    };
                }

                _localizers[ty] = action;
            }

            return action;
        }
    }
}