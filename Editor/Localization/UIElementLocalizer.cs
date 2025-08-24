#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEditor.UIElements;
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
                var textProp = ty.GetProperty("text");
                var labelProp = ty.GetProperty("label");
               
                if (textProp == null && labelProp == null)
                {
                    action = null;
                }
                else
                {
                    action = elem =>
                    {
                        var key = default(string);
                        var prop = default(PropertyInfo);

                        if (textProp != null && textProp.GetMethod != null && textProp.SetMethod != null && string.IsNullOrEmpty(key))
                        {
                            key = textProp.GetValue(elem) as string;
                            prop = textProp;
                        }
                        if (labelProp != null && labelProp.GetMethod != null && labelProp.SetMethod != null && string.IsNullOrEmpty(key))
                        {
                            key = labelProp.GetValue(elem) as string;
                            prop = labelProp;
                        }
                        
                        if (key != null)
                        {
                            return () =>
                            {
                                var new_label = _localizer.GetLocalizedString(key);
                                if (!_localizer.TryGetLocalizedString(key + ":tooltip", out var tooltip))
                                {
                                    tooltip = null;
                                }

                                prop.SetValue(elem, new_label);
                                elem.tooltip = tooltip;

                                if (elem is PropertyField pf)
                                {
                                    DeferredBindLabel(pf, key);
                                }
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

        private void DeferredBindLabel(PropertyField pf, string key)
        {
            // Workaround: PropertyField's label property doesn't work for certain types of controls, notably Sliders.
            // To work around this, we wait for the label to appear (checking on GeometryChangeEvents) and force-change
            // the label.
            // To avoid infinitely applying these, we deregister the callback after two editor frames.

            EditorApplication.CallbackFunction cb = () =>
            {
                var maybeLabel = pf.Children().FirstOrDefault()?.Children()?.FirstOrDefault();
                if (maybeLabel != null && maybeLabel is Label l && l.ClassListContains("unity-base-field__label"))
                {
                    l.text = _localizer.GetLocalizedString(key);
                }
            };

            // Sometimes this works after one frame, sometimes is needs two...
            EditorApplication.delayCall += cb;
            EditorApplication.delayCall += () => EditorApplication.delayCall += cb;
        }
    }
}