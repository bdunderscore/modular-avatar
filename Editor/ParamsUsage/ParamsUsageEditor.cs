#region

using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ParamsUsageEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

        private VisualElement _root;
        private VisualElement _entryTemplate;
        private VisualElement _usageBoxContainer;
        private VisualElement _legendContainer;

        private bool _visible = false;

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;
                _visible = value;

                if (_visible) Recalculate();
            }
        }

        private void OnEnable()
        {
            #if UNITY_2022_1_OR_NEWER
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            #endif
            Recalculate();
        }

#if UNITY_2022_1_OR_NEWER
        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            Recalculate();
        }

        private void OnDisable()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
        }
#endif

        protected override VisualElement CreateInnerInspectorGUI()
        {
            _root = uxml.CloneTree();
            _root.styleSheets.Add(uss);
            Localization.L.LocalizeUIElements(_root);

            _legendContainer = _root.Q<VisualElement>("Legend");
            _usageBoxContainer = _root.Q<VisualElement>("UsageBox");

            Recalculate();

            return _root;
        }

        protected override void OnInnerInspectorGUI()
        {
            // no-op
        }

        private static IEnumerable<Color> Colors()
        {
            // Spiral inwards on an HSV scale
            float h_step = 0.33f;
            float h_step_mult = 0.8f;
            float h_step_min = 0.05f;

            float v_mult = 0.98f;

            float h = 0;
            float s = 1;
            float v = 0.9f;

            while (true)
            {
                yield return Color.HSVToRGB(h, s, v);

                h = (h + h_step) % 1;
                h_step = h_step_min + ((h_step - h_step_min) * h_step_mult);
                v *= v_mult;
            }
        }

        private void Recalculate()
        {
            if (_root == null || !_visible) return;

            var ctx = serializedObject.context as GameObject;

            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(ctx.transform)?.gameObject;
            if (ctx == null || avatarRoot == null) return;

            var orderedPlugins = ParameterInfo.ForUI.GetParametersForObject(ctx)
                .GroupBy(p => p.Plugin)
                .Select(group => (group.Key, group.Sum(p => p.BitUsage)))
                .OrderBy(group => group.Key.DisplayName)
                .ToList();

            var byPlugin = orderedPlugins
                .Zip(Colors(), (kv, color) => (kv.Key.DisplayName, kv.Item2, kv.Key.ThemeColor ?? color))
                .ToList();

            int totalUsage = byPlugin.Sum(kv => kv.Item2);

            int avatarTotalUsage =
                ParameterInfo.ForUI.GetParametersForObject(avatarRoot).Sum(p => p.BitUsage);

            int freeSpace = VRCExpressionParameters.MAX_PARAMETER_COST - avatarTotalUsage;

            float avatarTotalPerc = avatarTotalUsage / (float)VRCExpressionParameters.MAX_PARAMETER_COST;
            float freeSpacePerc = freeSpace / (float)VRCExpressionParameters.MAX_PARAMETER_COST;

            if (avatarTotalUsage > totalUsage)
            {
                byPlugin.Add((Localization.S("ma_info.param_usage_ui.other_objects"), avatarTotalUsage - totalUsage,
                    Color.gray));
            }

            var bits_template = Localization.S("ma_info.param_usage_ui.bits_template");
            byPlugin = byPlugin.Select((tuple, _) =>
                (string.Format(bits_template, tuple.Item1, tuple.Item2), tuple.Item2, tuple.Item3)).ToList();

            if (freeSpace > 0)
            {
                var free_space_label = Localization.S("ma_info.param_usage_ui.free_space");
                byPlugin.Add((string.Format(free_space_label, freeSpace), freeSpace, Color.white));
            }

            foreach (var child in _legendContainer.Children().ToList())
            {
                child.RemoveFromHierarchy();
            }

            foreach (var child in _usageBoxContainer.Children().ToList())
            {
                child.RemoveFromHierarchy();
            }

            foreach (var (label, usage, color) in byPlugin)
            {
                var colorBar = new VisualElement();
                colorBar.style.backgroundColor = color;
                colorBar.style.width =
                    new StyleLength(new Length(100.0f * usage / (float)VRCExpressionParameters.MAX_PARAMETER_COST,
                        LengthUnit.Percent));
                _usageBoxContainer.Add(colorBar);

                var entry = new VisualElement();
                _legendContainer.Add(entry);
                entry.AddToClassList("Entry");

                var icon_outer = new VisualElement();
                icon_outer.AddToClassList("IconOuter");
                entry.Add(icon_outer);

                var icon_inner = new VisualElement();
                icon_inner.AddToClassList("IconInner");
                icon_outer.Add(icon_inner);
                icon_inner.style.backgroundColor = color;

                var pluginLabel = new Label(label);
                entry.Add(pluginLabel);

                entry.style.borderBottomColor = color;
                entry.style.borderTopColor = color;
                entry.style.borderLeftColor = color;
                entry.style.borderRightColor = color;

                colorBar.style.borderBottomColor = color;
                colorBar.style.borderTopColor = color;
                colorBar.style.borderLeftColor = color;
                colorBar.style.borderRightColor = color;

                SetMouseHover(entry, colorBar);
                SetMouseHover(colorBar, entry);
            }
        }

        private void SetMouseHover(VisualElement src, VisualElement other)
        {
            src.RegisterCallback<MouseEnterEvent>(ev => { other.AddToClassList("Hovering"); });

            src.RegisterCallback<MouseLeaveEvent>(ev => { other.RemoveFromClassList("Hovering"); });
        }
    }
}