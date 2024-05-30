#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomPropertyDrawer(typeof(ChangedShape))]
    public class ChangedShapeEditor : PropertyDrawer
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/ShapeChanger/";
        const string UxmlPath = Root + "ChangedShapeEditor.uxml";
        const string UssPath = Root + "ShapeChangerStyles.uss";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.BindProperty(property);

            uxml.Q<PropertyField>("f-change-type").RegisterCallback<ChangeEvent<string>>(
                e =>
                {
                    if (e.newValue == "Delete")
                    {
                        uxml.AddToClassList("change-type-delete");
                    }
                    else
                    {
                        uxml.RemoveFromClassList("change-type-delete");
                    }
                }
            );

            return uxml;
        }
    }
}