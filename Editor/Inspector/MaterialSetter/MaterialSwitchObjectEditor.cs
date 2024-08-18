#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
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
            
            var f_material_index = uxml.Q<DropdownField>("f-material-index");
            
            var f_object = uxml.Q<ObjectField>("f-object");
            f_object.objectType = typeof(Renderer);
            f_object.allowSceneObjects = true;
            
            var f_target_object = uxml.Q<ObjectField>("f-obj-target-object");
            var f_reference_path = uxml.Q<TextField>("f-obj-ref-path");
            
            f_object.RegisterValueChangedCallback(evt =>
            {
                var gameObj = (evt.newValue as Renderer)?.gameObject;

                if (gameObj == null)
                {
                    f_target_object.value = null;
                    f_reference_path.value = "";
                }
                else
                {
                    var path = RuntimeUtil.AvatarRootPath(gameObj);

                    f_reference_path.value = path;
                    if (path == "")
                    {
                        f_target_object.value = null;
                    }
                    else
                    {
                        f_target_object.value = gameObj;
                    }
                }

                EditorApplication.delayCall += UpdateMaterialDropdown;
            });
            UpdateMaterialDropdown();

            f_target_object.RegisterValueChangedCallback(_ => UpdateVisualTarget());
            f_reference_path.RegisterValueChangedCallback(_ => UpdateVisualTarget());
            
            // Link dropdown to material index field
            var f_material_index_int = uxml.Q<IntegerField>("f-material-index-int");
            f_material_index_int.RegisterValueChangedCallback(evt =>
            {
                f_material_index.SetValueWithoutNotify("" + evt.newValue);
            });
            
            f_material_index.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null && int.TryParse(evt.newValue, out var i))
                {
                    f_material_index_int.value = i;
                }
            });

            return uxml;

            void UpdateVisualTarget()
            {
                var setter = property.serializedObject.targetObject as ModularAvatarMaterialSetter;
                var renderer = GetTargetRenderer(AvatarObjectReference.Get(property.FindPropertyRelative("Object")));
                var overrideRenderer = GetTargetRenderer(setter?.targetRenderer.Get(setter));

                f_object.SetEnabled(overrideRenderer == null);
                f_object.SetValueWithoutNotify(overrideRenderer ?? renderer);

                Renderer GetTargetRenderer(GameObject obj)
                {
                    try
                    {
                        return obj?.GetComponent<Renderer>();
                    }
                    catch (MissingComponentException e)
                    {
                        return null;
                    }
                }
            }
            
            void UpdateMaterialDropdown()
            {
                var setter = property.serializedObject.targetObject as ModularAvatarMaterialSetter;
                var sharedMaterials = GetSharedMaterials(AvatarObjectReference.Get(property.FindPropertyRelative("Object")));
                var overrideSharedMaterials = GetSharedMaterials(setter?.targetRenderer.Get(setter));

                sharedMaterials = overrideSharedMaterials ?? sharedMaterials;
                if (sharedMaterials != null)
                {
                    var matCount = sharedMaterials.Length;
                    
                    f_material_index.SetEnabled(true);
                    
                    f_material_index.choices.Clear();
                    for (int i = 0; i < matCount; i++)
                    {
                        f_material_index.choices.Add(i.ToString());
                    }

                    f_material_index.formatListItemCallback = idx_s =>
                    {
                        if (string.IsNullOrWhiteSpace(idx_s)) return "";
                        
                        var idx = int.Parse(idx_s);
                        if (idx < 0 || idx >= sharedMaterials.Length)
                        {
                            return idx + ": <???>";
                        }
                        else if (sharedMaterials[idx] == null)
                        {
                            return idx + ": <none>";
                        }
                        else
                        {
                            return idx + ": " + sharedMaterials[idx].name;
                        }
                    };
                    f_material_index.formatSelectedValueCallback = f_material_index.formatListItemCallback;
                }
                else
                {
                    f_material_index.SetEnabled(false);
                    if (f_material_index.choices.Count == 0)
                    {
                        f_material_index.choices.Add("0");
                    }
                    
                    f_material_index.formatListItemCallback = _ => "<Missing Renderer>";
                    f_material_index.formatSelectedValueCallback = f_material_index.formatListItemCallback;
                }

                Material[] GetSharedMaterials(GameObject obj)
                {
                    try
                    {
                        return obj?.GetComponent<Renderer>()?.sharedMaterials;
                    }
                    catch (MissingComponentException e)
                    {
                        return null;
                    }
                }
            }
        }
    }
}