#region

using System;
using System.Collections.Generic;
using System.Reflection;
using nadena.dev.ndmf.localization;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class UIElementLocalizer
    {
        private static Dictionary<Type, Func<VisualElement, Action>> _localizers =
            new Dictionary<Type, Func<VisualElement, Action>>();

        private readonly Localizer _localizer;

        public UIElementLocalizer(Localizer localizer)
        {
            _localizer = localizer;
        }

        internal void Localize(VisualElement elem)
        {
            WalkTree(elem);
            LanguagePrefs.ApplyFontPreferences(elem);
        }

        private void WalkTree(VisualElement elem)
        {
            var ty = elem.GetType();
            
            if (elem.ClassListContains("ndmf-tr"))
            {
                var op = GetLocalizationOperation(ty);
                if (op != null)
                {
                    var action = op(elem);
                    LanguagePrefs.RegisterLanguageChangeCallback(elem, _elem => action());
                    action();
                }
            }

            foreach (var child in elem.Children())
            {
                WalkTree(child);
            }
        }
        
        private Func<VisualElement, Action> GetLocalizationOperation(Type ty)
        {
            if (!_localizers.TryGetValue(ty, out var action))
            {
                PropertyInfo m_label = ty.GetProperty("text") ?? ty.GetProperty("label");
               
                if (m_label == null)
                {
                    action = null;
                }
                else
                {
                    action = elem =>
                    {
                        var key = m_label.GetValue(elem) as string;
                        
                        if (key != null)
                        {
                            return () =>
                            {
                                var new_label = _localizer.GetLocalizedString(key);
                                if (!_localizer.TryGetLocalizedString(key + ":tooltip", out var tooltip))
                                {
                                    tooltip = null;
                                }

                                m_label.SetValue(elem, new_label);
                                elem.tooltip = tooltip;
                            };
                        }
                        else
                        {
                            return () => { };
                        }
                    };
                }

                _localizers[ty] = action;
            }

            return action;
        }
    }
}