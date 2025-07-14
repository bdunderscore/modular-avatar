#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(MeshDeleteObject))]
    public class MeshDeleteObjectEditor : PropertyDrawer
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshDeleter/";
        private const string UxmlPath = Root + "MeshDeleteObjectEditor.uxml";
        private const string UssPath = Root + "MeshDeleterStyles.uss";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.BindProperty(property);

            var f_object = uxml.Q<PropertyField>("f-object");
            var f_material_index = uxml.Q<IntegerField>("f-material-index");
            var f_material_index_dropdown = uxml.Q<DropdownField>("f-material-index-dropdown");
            var f_material_index_original = uxml.Q<ObjectField>("f-material-index-original");
            MaterialSlotSelector.Setup(property.FindPropertyRelative("Object"), f_object, f_material_index, f_material_index_dropdown, f_material_index_original);

            return uxml;
        }
    }
}
