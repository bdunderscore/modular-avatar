#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomPropertyDrawer(typeof(MaterialSwitchObject))]
    public class MaterialSwitchObjectEditor : PropertyDrawer
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MaterialSetter/";
        private const string UxmlPath = Root + "MaterialSwitchObjectEditor.uxml";
        private const string UssPath = Root + "MaterialSetterStyles.uss";
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.BindProperty(property);
            
            var f_material_index = uxml.Q<IntegerField>("f-material-index");
            var f_material_index_dropdown = uxml.Q<DropdownField>("f-material-index-dropdown");
            var f_material_index_original = uxml.Q<ObjectField>("f-material-index-original");
            
            var f_object = uxml.Q<PropertyField>("f-object");
            
            f_object.RegisterValueChangeCallback(evt =>
            {
                EditorApplication.delayCall += UpdateMaterialDropdown;
            });
            UpdateMaterialDropdown();

            // Link dropdown and original field to material index field
            f_material_index.RegisterValueChangedCallback(evt =>
            {
                f_material_index_dropdown.SetValueWithoutNotify(evt.newValue.ToString());
                UpdateOriginalMaterial();
            });
            f_material_index_dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null && int.TryParse(evt.newValue, out var i))
                {
                    f_material_index.value = i;
                }
            });
            f_material_index_original.SetEnabled(false);

            return uxml;

            void UpdateMaterialDropdown()
            {
                var sharedMaterials = GetSharedMaterials();

                if (sharedMaterials != null)
                {
                    var matCount = sharedMaterials.Length;
                    
                    f_material_index_dropdown.SetEnabled(true);
                    
                    f_material_index_dropdown.choices.Clear();
                    for (int i = 0; i < matCount; i++)
                    {
                        f_material_index_dropdown.choices.Add(i.ToString());
                    }

                    f_material_index_dropdown.formatListItemCallback = idx_s =>
                    {
                        if (string.IsNullOrWhiteSpace(idx_s)) return "";
                        
                        var idx = int.Parse(idx_s);
                        if (idx < 0 || idx >= sharedMaterials.Length)
                        {
                            return $"<color=\"red\">Element {idx_s}: <???></color>";
                        }
                        else if (sharedMaterials[idx] == null)
                        {
                            return $"Element {idx_s}: <None>";
                        }
                        else
                        {
                            return $"Element {idx_s}: {sharedMaterials[idx].name}";
                        }
                    };
                    f_material_index_dropdown.formatSelectedValueCallback = idx_s =>
                    {
                        if (string.IsNullOrWhiteSpace(idx_s)) return "";

                        var idx = int.Parse(idx_s);
                        if (idx < 0 || idx >= sharedMaterials.Length)
                        {
                            return $"<color=\"red\">Element {idx_s}</color>";
                        }
                        else
                        {
                            return $"Element {idx_s}";
                        }
                    };
                }
                else
                {
                    f_material_index_dropdown.SetEnabled(false);
                    if (f_material_index_dropdown.choices.Count == 0)
                    {
                        f_material_index_dropdown.choices.Add("0");
                    }
                    
                    f_material_index_dropdown.formatListItemCallback = idx_s => "<Missing Renderer>";
                    f_material_index_dropdown.formatSelectedValueCallback = f_material_index_dropdown.formatListItemCallback;
                }

                UpdateOriginalMaterial();
            }

            void UpdateOriginalMaterial()
            {
                var sharedMaterials = GetSharedMaterials();

                if (sharedMaterials != null)
                {
                    var idx = f_material_index.value;
                    if (idx < 0 || idx >= sharedMaterials.Length)
                    {
                        f_material_index_original.SetValueWithoutNotify(null);
                    }
                    else
                    {
                        f_material_index_original.SetValueWithoutNotify(sharedMaterials[idx]);
                    }
                }
                else
                {
                    f_material_index_original.SetValueWithoutNotify(null);
                }
            }

            Material[] GetSharedMaterials()
            {
                var targetObject = AvatarObjectReference.Get(property.FindPropertyRelative("Object"));
                try
                {
                    return targetObject?.GetComponent<Renderer>()?.sharedMaterials;
                }
                catch (MissingComponentException)
                {
                    return null;
                }
            }
        }
    }
}