using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarOutfitRoot))]
    internal class OutfitRootEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/OutfitRoot/";
        private const string UxmlPath = Root + "OutfitRootEditor.uxml";
        private const string UssPath = Root + "OutfitRootEditor.uss";

        private readonly MergeArmaturePositionResetOptions _resetOptions = new();

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath));

            var armatureRootProperty = serializedObject.FindProperty(nameof(ModularAvatarOutfitRoot.armatureRoot));
            var armatureRootField = root.Q<PropertyField>("armature-root");
            var adjustNames = root.Q<Button>("adjust-names");
            var matchScaleAdjusters = root.Q<Button>("match-scale-adjusters");
            var resetSection = root.Q<VisualElement>("reset-position");
            var heuristicScale = root.Q<Toggle>("heuristic-scale");
            var convertATPose = root.Q<Toggle>("convert-at-pose");
            var adjustRotation = root.Q<Toggle>("adjust-rotation");
            var adjustScale = root.Q<Toggle>("adjust-scale");
            var executeReset = root.Q<Button>("execute-reset");

            adjustNames.clicked += () =>
            {
                serializedObject.ApplyModifiedProperties();
                MergeArmatureInspectorTools.AdjustNames(
                    ((ModularAvatarOutfitRoot)target).armatureRoot);
            };

            matchScaleAdjusters.clicked += () =>
            {
                serializedObject.ApplyModifiedProperties();
                MergeArmatureInspectorTools.MatchScaleAdjusters(
                    ((ModularAvatarOutfitRoot)target).armatureRoot);
            };

            BindToggle(heuristicScale, _resetOptions.HeuristicRootScale,
                value => _resetOptions.HeuristicRootScale = value);
            BindToggle(convertATPose, _resetOptions.ConvertATPose,
                value => _resetOptions.ConvertATPose = value);
            BindToggle(adjustRotation, _resetOptions.AdjustRotation,
                value => _resetOptions.AdjustRotation = value);
            BindToggle(adjustScale, _resetOptions.AdjustScale,
                value => _resetOptions.AdjustScale = value);

            executeReset.clicked += () =>
            {
                serializedObject.ApplyModifiedProperties();
                MergeArmatureInspectorTools.ForcePositionToBaseAvatar(
                    ((ModularAvatarOutfitRoot)target).armatureRoot, _resetOptions);
            };

            void UpdateEnabledState()
            {
                serializedObject.UpdateIfRequiredOrScript();
                var outfitRoot = target as ModularAvatarOutfitRoot;
                var enabled = outfitRoot != null
                              && MergeArmatureInspectorTools.HasValidTarget(outfitRoot.armatureRoot);
                adjustNames.SetEnabled(enabled);
                matchScaleAdjusters.SetEnabled(enabled);
                resetSection.SetEnabled(enabled);
            }

            root.TrackPropertyValue(armatureRootProperty, _ => UpdateEnabledState());
            Localization.UI.Localize(root);
            armatureRootField.tooltip = Localization.S("outfit_root.armature_root.tooltip");
            adjustNames.tooltip = Localization.S("merge_armature.adjust_names.tooltip");
            heuristicScale.tooltip = Localization.S("merge_armature.reset_pos.heuristic_scale.tooltip");
            matchScaleAdjusters.tooltip =
                Localization.S("merge_armature.match_scale_adjusters.tooltip");
            root.Bind(serializedObject);
            UpdateEnabledState();

            return root;
        }

        private static void BindToggle(Toggle toggle, bool initialValue,
            Action<bool> setter)
        {
            toggle.SetValueWithoutNotify(initialValue);
            toggle.RegisterValueChangedCallback(evt => setter(evt.newValue));
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }
    }
}