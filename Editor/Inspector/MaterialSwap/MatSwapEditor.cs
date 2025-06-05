#region

using UnityEditor;
using UnityEditor.UIElements;
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

            return uxml;
        }
    }
}
