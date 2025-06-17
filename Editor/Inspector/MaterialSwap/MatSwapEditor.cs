#region

using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomPropertyDrawer(typeof(MatSwap))]
    public class MatSwapEditor : PropertyDrawer
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MaterialSwap/";
        private const string UxmlPath = Root + "MatSwapEditor.uxml";
        private const string UssPath = Root + "MaterialSwapStyles.uss";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.BindProperty(property);

            var fromField = uxml.Q<PropertyField>("from-field");
            var fromDropdown = uxml.Q<DropdownField>("from-dropdown");
            var fromProperty = property.FindPropertyRelative("From");
            fromDropdown.RegisterCallback<PointerDownEvent>(_ =>
            {
                var swap = property.serializedObject.targetObject as ModularAvatarMaterialSwap;
                if (swap == null)
                {
                    return;
                }
                var root = swap.Root.Get(swap)?.transform ?? RuntimeUtil.FindAvatarTransformInParents(swap.transform);
                if (root == null)
                {
                    return;
                }

                var menu = new GenericDropdownMenu();
                foreach (var material in root.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(x => x.sharedMaterials)
                    .Where(x => x != null)
                    .Distinct())
                {
                    menu.AddItem(material.name, material == fromProperty.objectReferenceValue, () =>
                    {
                        fromProperty.serializedObject.Update();
                        fromProperty.objectReferenceValue = material;
                        fromProperty.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.DropDown(fromField.worldBound, fromDropdown);
            });

            return uxml;
        }
    }
}
