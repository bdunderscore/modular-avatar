#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomPropertyDrawer(typeof(ToggledObject))]
    public class ToggledObjectEditor : PropertyDrawer
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/ObjectToggle/";
        private const string UxmlPath = Root + "ToggledObjectEditor.uxml";
        private const string UssPath = Root + "ObjectSwitcherStyles.uss";

        private const string V_On = "ON";
        private const string V_Off = "OFF";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.BindProperty(property);

            var f_active = uxml.Q<Toggle>("f-active");
            var f_active_dropdown = uxml.Q<DropdownField>("f-active-dropdown");

            f_active_dropdown.choices.Add(V_On);
            f_active_dropdown.choices.Add(V_Off);

            f_active.RegisterValueChangedCallback(evt =>
            {
                f_active_dropdown.SetValueWithoutNotify(evt.newValue ? V_On : V_Off);
            });
            f_active_dropdown.RegisterValueChangedCallback(evt =>
            {
                f_active.value = evt.newValue == V_On;
            });

            return uxml;
        }
    }
}