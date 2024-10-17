#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.ui;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ParamsUsageWindow : EditorWindow
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

#if UNITY_2022_1_OR_NEWER
                if (_visible)
                {
                    Recalculate();
                    ObjectChangeEvents.changesPublished += OnChangesPublished;
                }
                else
                {
                    ObjectChangeEvents.changesPublished -= OnChangesPublished;
                }
#endif
            }
        }

        private void OnBecameVisible()
        {
            Visible = true;
        }

        private void OnBecameInvisible()
        {
            Visible = false;
        }

        private void OnSelectionChange()
        {
            if (Visible)
            {
                Recalculate();
            }
        }

#if UNITY_2022_1_OR_NEWER
        [MenuItem(UnityMenuItems.TopMenu_EnableInfo, false, UnityMenuItems.TopMenu_EnableInfoOrder)]
        public static void ShowWindow()
        {
            var window = GetWindow<ParamsUsageWindow>();
            window.titleContent = new GUIContent("MA Information");
            window.Show();
        }

        [MenuItem(UnityMenuItems.GameObject_EnableInfo, false, UnityMenuItems.GameObject_EnableInfoOrder)]
        public static void ShowWindowFromGameObject()
        {
            ShowWindow();
        }

        [MenuItem(UnityMenuItems.GameObject_EnableInfo, true, UnityMenuItems.GameObject_EnableInfoOrder)]
        public static bool ValidateShowWindowFromGameObject()
        {
            return Selection.gameObjects.Length == 1;
        }
        
        private bool _delayPending = false;

        private void DelayRecalculate()
        {
            _delayPending = false;
            Recalculate();
        }
        
        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (_root == null || !_visible) return;
            if (!_delayPending) EditorApplication.delayCall += DelayRecalculate;
            _delayPending = true;
        }
#endif

        bool GUIIsReady()
        {
            try
            {
                return uxml != null && EditorStyles.label != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        protected void CreateGUI()
        {
            if (!GUIIsReady())
            {
                // After domain reload, the uxml field (and EditorStyles, etc) isn't initialized immediately.
                // Try again in the next frame.
                EditorApplication.delayCall += CreateGUI;
                return;
            }
            
            _root = uxml.CloneTree();

            rootVisualElement.Add(_root);
            _root.styleSheets.Add(uss);
            Localization.L.LocalizeUIElements(_root);
 
            _legendContainer = _root.Q<VisualElement>("Legend");
            _usageBoxContainer = _root.Q<VisualElement>("UsageBox");

            Recalculate(); 
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

            var objects = Selection.gameObjects;
            var target = objects.Length == 1 ? objects[0] : null;
            var avatarRoot = target != null
                ? RuntimeUtil.FindAvatarTransformInParents(target.transform)?.gameObject
                : null;

            var outerbox = _root.Q<VisualElement>("Outerbox");
            if (avatarRoot == null)
            {
                outerbox.AddToClassList("no-data");
                return;
            }
            else
            {
                outerbox.RemoveFromClassList("no-data");
            }

            var orderedPlugins = ParameterInfo.ForUI.GetParametersForObject(target)
                .GroupBy(p => p.Plugin)
                .Select(group => (group.Key, group.Sum(p => p.BitUsage)))
                .Where((kv, index) => kv.Item2 > 0)
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