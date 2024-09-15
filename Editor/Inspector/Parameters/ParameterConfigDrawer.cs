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

            var f_type = root.Q<DropdownField>("f-type");
            var f_sync_type = root.Q<DropdownField>("f-sync-type");
            var f_is_prefix = root.Q<VisualElement>("f-is-prefix");
            SetupPairedDropdownField(
                root,
                f_type,
                f_sync_type,
                f_is_prefix,
                ("Bool", "False", "params.syncmode.Bool"),
                ("Float", "False", "params.syncmode.Float"),
                ("Int", "False", "params.syncmode.Int"),
                ("Not Synced", "False", "params.syncmode.NotSynced"),
                (null, "True", "params.syncmode.PhysBonesPrefix")
            );

            var f_default = root.Q<DefaultValueField>();
            f_default.OnUpdateSyncType((ParameterSyncType)f_sync_type.index);
            f_sync_type.RegisterValueChangedCallback(evt => f_default.OnUpdateSyncType((ParameterSyncType)f_sync_type.index));

            var f_synced = root.Q<Toggle>("f-synced");
            var f_local_only = root.Q<Toggle>("f-local-only");

            // Invert f_local_only and f_synced
            f_local_only.RegisterValueChangedCallback(evt => { f_synced.SetValueWithoutNotify(!evt.newValue); });

            f_synced.RegisterValueChangedCallback(evt => { f_local_only.value = !evt.newValue; });

            var internalParamAccessor = root.Q<Toggle>("f-internal-parameter");
            internalParamAccessor.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    root.AddToClassList("st-internal-parameter");
                else
                    root.RemoveFromClassList("st-internal-parameter");
            });

            root.Q<VisualElement>("remap-to-group-disabled").SetEnabled(false);

            var name = root.Q<TextField>("f-name");
            var remapTo = root.Q<TextField>("f-remap-to");
            var remapToInner = remapTo.Q<TextElement>();
            var remapToPlaceholder = root.Q<Label>("f-remap-to-placeholder");
            remapToPlaceholder.pickingMode = PickingMode.Ignore;

            Action updateRemapToPlaceholder = () =>
            {
                if (string.IsNullOrWhiteSpace(remapTo.value))
                    remapToPlaceholder.text = name.value;
                else
                    remapToPlaceholder.text = "";
            };

            name.RegisterValueChangedCallback(evt => { updateRemapToPlaceholder(); });

            remapTo.RegisterValueChangedCallback(evt => { updateRemapToPlaceholder(); });

            remapToPlaceholder.RemoveFromHierarchy();
            remapToInner.Add(remapToPlaceholder);

            updateRemapToPlaceholder();

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