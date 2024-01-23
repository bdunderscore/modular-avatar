#if MA_VRCSDK3_AVATARS && UNITY_2022_1_OR_NEWER

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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
            
            root.Q<PropertyField>("internalParameter").RegisterValueChangeCallback(evt =>
            {
                remapTo.style.display = evt.changedProperty.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                remapToPlaceholder.style.display = evt.changedProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            return root;
        }

    }
}
#endif