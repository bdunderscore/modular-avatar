#if MA_VRCSDK3_AVATARS && UNITY_2022_1_OR_NEWER

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

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

            var foldout = root.Q<Foldout>();
            var foldoutLabel = foldout?.Q<Label>();
            if (foldoutLabel != null)
            {
                foldoutLabel.bindingPath = "nameOrPrefix";
            }
            
            var miniDisplay = root.Q<VisualElement>("MiniDisplay");
            miniDisplay.RemoveFromHierarchy();
            foldoutLabel.parent.Add(miniDisplay);
            miniDisplay.styleSheets.Add(uss);

            var isPrefixProp = root.Q<PropertyField>("isPrefix");
            bool isPrefix = false;
            Action evaluateMiniDisplay = () =>
            {
                miniDisplay.style.display = (isPrefix || foldout.value) ? DisplayStyle.None : DisplayStyle.Flex;
            };
            
            
            foldout.RegisterValueChangedCallback(evt => evaluateMiniDisplay());
            
            isPrefixProp.RegisterValueChangeCallback(evt =>
            {
                var value = evt.changedProperty.boolValue;
                if (value)
                {
                    root.AddToClassList("ParameterConfig__isPrefix");
                }
                else
                {
                    root.RemoveFromClassList("ParameterConfig__isPrefix");
                }

                isPrefix = value;
                evaluateMiniDisplay();
            });
   
            var remapTo = root.Q<PropertyField>("remapTo");
            var remapToPlaceholder = root.Q<TextField>("remapToPlaceholder");
            remapToPlaceholder.SetEnabled(false);

            remapToPlaceholder.labelElement.AddToClassList("ndmf-tr");
            Localization.UI.Localize(remapToPlaceholder.labelElement);
            
            root.Q<PropertyField>("internalParameter").RegisterValueChangeCallback(evt =>
            {
                remapTo.style.display = evt.changedProperty.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                remapToPlaceholder.style.display = evt.changedProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            
            
            // This is a bit of a hack, but I'm not sure of another way to properly align property labels with a custom
            // field, when we only want to manipulate a subset of fields on an object...
            var defaultValueField = root.Q<VisualElement>("innerDefaultValueField"); // create ahead of time so it's bound...
            
            // Then move it into the property field once the property field has created its inner controls
            var defaultValueProp = root.Q<PropertyField>("defaultValueProp");
            defaultValueProp.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var floatField = defaultValueProp.Q<FloatField>();
                var innerField = floatField?.Q<DefaultValueField>();

                if (floatField != null && innerField == null)
                {
                    defaultValueField.RemoveFromHierarchy();
                    floatField.contentContainer.Add(defaultValueField);
                }
            });

            return root;
        }

    }
}
#endif