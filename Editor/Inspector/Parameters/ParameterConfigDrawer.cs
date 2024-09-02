#if MA_VRCSDK3_AVATARS && UNITY_2022_1_OR_NEWER

using System;
using UnityEditor;
using UnityEngine.UIElements;
using Toggle = UnityEngine.UIElements.Toggle;

namespace nadena.dev.modular_avatar.core.editor.Parameters
{
    [CustomPropertyDrawer(typeof(ParameterConfig))]
    internal class ParameterConfigDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var rootPath = "Packages/nadena.dev.modular-avatar/Editor/Inspector/Parameters";
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(rootPath + "/Parameters.uss");
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(rootPath + "/ParameterConfigDrawer.uxml");

            var root = uxml.CloneTree();
            Localization.UI.Localize(root);
            root.styleSheets.Add(uss);

            var proot = root.Q<VisualElement>("Root");
            var type_field = proot.Q<DropdownField>("f-type");

            var f_sync_type = proot.Q<VisualElement>("f-sync-type");
            SetupPairedDropdownField(
                proot,
                type_field,
                f_sync_type,
                proot.Q<VisualElement>("f-is-prefix"),
                ("Bool", "False", "params.syncmode.Bool"),
                ("Float", "False", "params.syncmode.Float"),
                ("Int", "False", "params.syncmode.Int"),
                ("Not Synced", "False", "params.syncmode.NotSynced"),
                (null, "True", "params.syncmode.PhysBonesPrefix")
            );

            f_sync_type.Q<DropdownField>().RegisterValueChangedCallback(evt =>
            {
                var is_anim_only = evt.newValue == "Not Synced";

                if (is_anim_only)
                    proot.AddToClassList("st-anim-only");
                else
                    proot.RemoveFromClassList("st-anim-only");
            });

            var f_synced = proot.Q<Toggle>("f-synced");
            var f_local_only = proot.Q<Toggle>("f-local-only");

            // Invert f_local_only and f_synced
            f_local_only.RegisterValueChangedCallback(evt => { f_synced.SetValueWithoutNotify(!evt.newValue); });

            f_synced.RegisterValueChangedCallback(evt => { f_local_only.value = !evt.newValue; });

            var internalParamAccessor = proot.Q<Toggle>("f-internal-parameter");
            internalParamAccessor.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    proot.AddToClassList("st-internal-parameter");
                else
                    proot.RemoveFromClassList("st-internal-parameter");
            });

            var remapTo = proot.Q<TextField>("f-remap-to");
            var defaultParam = proot.Q<Label>("f-default-param");
            var name = proot.Q<TextField>("f-name");
            var remapToInner = remapTo.Q<TextElement>();

            Action updateDefaultParam = () =>
            {
                if (string.IsNullOrWhiteSpace(remapTo.value))
                    defaultParam.text = name.value;
                else
                    defaultParam.text = "";
            };

            name.RegisterValueChangedCallback(evt => { updateDefaultParam(); });

            remapTo.RegisterValueChangedCallback(evt => { updateDefaultParam(); });

            defaultParam.RemoveFromHierarchy();
            remapToInner.Add(defaultParam);

            updateDefaultParam();

            return root;
        }

        private interface Accessor
        {
            Action<string> OnValueChanged { get; set; }
            string Value { get; set; }
        }

        private class ToggleAccessor : Accessor
        {
            private readonly Toggle _toggle;

            public ToggleAccessor(Toggle toggle)
            {
                _toggle = toggle;
                _toggle.RegisterValueChangedCallback(evt => OnValueChanged?.Invoke(evt.newValue.ToString()));
            }

            public Action<string> OnValueChanged { get; set; }

            public string Value
            {
                get => _toggle.value.ToString();
                set => _toggle.value = value == "True";
            }
        }

        private class DropdownAccessor : Accessor
        {
            private readonly DropdownField _dropdown;

            public DropdownAccessor(DropdownField dropdown)
            {
                _dropdown = dropdown;
                _dropdown.RegisterValueChangedCallback(evt => OnValueChanged?.Invoke(evt.newValue));
            }

            public Action<string> OnValueChanged { get; set; }

            public string Value
            {
                get => _dropdown.value;
                set => _dropdown.value = value;
            }
        }

        private Accessor GetAccessor(VisualElement elem)
        {
            var toggle = elem.Q<Toggle>();
            if (toggle != null) return new ToggleAccessor(toggle);

            var dropdown = elem.Q<DropdownField>();
            if (dropdown != null)
            {
                return new DropdownAccessor(dropdown);
            }

            throw new ArgumentException("Unsupported element type");
        }

        private void SetupPairedDropdownField(
            VisualElement root,
            DropdownField target,
            VisualElement v_type,
            VisualElement v_pbPrefix,
            // p1, p2, localization key
            params (string, string, string)[] choices
        )
        {
            var p_type = GetAccessor(v_type);
            var p_prefix = GetAccessor(v_pbPrefix);

            v_type.style.display = DisplayStyle.None;
            v_pbPrefix.style.display = DisplayStyle.None;

            for (var i = 0; i < choices.Length; i++) target.choices.Add("" + i);

            target.formatListItemCallback = s_n =>
            {
                if (int.TryParse(s_n, out var n) && n >= 0 && n < choices.Length)
                {
                    return Localization.S(choices[n].Item3);
                }
                else
                {
                    return "";
                }
            };
            target.formatSelectedValueCallback = target.formatListItemCallback;

            var inLoop = false;
            string current_type_class = null;

            target.RegisterValueChangedCallback(evt =>
            {
                if (inLoop) return;

                if (int.TryParse(evt.newValue, out var n) && n >= 0 && n < choices.Length)
                {
                    p_type.Value = choices[n].Item1;
                    p_prefix.Value = choices[n].Item2;
                }
                else
                {
                    p_type.Value = "";
                    p_prefix.Value = "";
                }
            });

            p_type.OnValueChanged = s =>
            {
                inLoop = true;
                try
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        var new_class = "st-ty-" + s.Replace(" ", "-");

                        root.RemoveFromClassList(current_type_class);
                        current_type_class = null;

                        root.AddToClassList(new_class);
                        current_type_class = new_class;
                    }

                    if (string.IsNullOrEmpty(s)) return;

                    for (var i = 0; i < choices.Length; i++)
                        if (choices[i].Item1 == s && (choices[i].Item2 == null || choices[i].Item2 == p_prefix.Value))
                        {
                            target.SetValueWithoutNotify("" + i);
                            break;
                        }
                }
                finally
                {
                    inLoop = false;
                }
            };

            p_prefix.OnValueChanged = s =>
            {
                inLoop = true;
                try
                {
                    if (string.IsNullOrEmpty(s)) return;

                    if (bool.TryParse(s, out var b))
                    {
                        if (b) root.AddToClassList("st-pb-prefix");
                        else root.RemoveFromClassList("st-pb-prefix");
                    }

                    for (var i = 0; i < choices.Length; i++)
                        if ((choices[i].Item1 == null || choices[i].Item1 == p_type.Value) && choices[i].Item2 == s)
                        {
                            target.SetValueWithoutNotify("" + i);
                            break;
                        }
                }
                finally
                {
                    inLoop = false;
                }
            };

            inLoop = true;
            for (var i = 0; i < choices.Length; i++)
                if (choices[i].Item1 == p_type.Value && choices[i].Item2 == p_prefix.Value)
                {
                    target.SetValueWithoutNotify("" + i);
                    break;
                }

            inLoop = false;
        }
    }
}
#endif