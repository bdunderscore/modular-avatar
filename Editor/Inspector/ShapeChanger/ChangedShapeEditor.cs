#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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

            var f_shape_name = uxml.Q<DropdownField>("f-shape-name");

            var f_object = uxml.Q<PropertyField>("f-object");

            f_object.RegisterValueChangeCallback(evt =>
            {
                EditorApplication.delayCall += UpdateShapeDropdown;
            });
            UpdateShapeDropdown();

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

            void UpdateShapeDropdown()
            {
                var targetObject = AvatarObjectReference.Get(property.FindPropertyRelative("Object"));
                List<string> shapeNames;
                try
                {
                    var mesh = targetObject?.GetComponent<SkinnedMeshRenderer>()?.sharedMesh;
                    shapeNames = mesh == null ? null : Enumerable.Range(0, mesh.blendShapeCount)
                        .Select(x => mesh.GetBlendShapeName(x))
                        .ToList();
                }
                catch (MissingComponentException)
                {
                    shapeNames = null;
                }

                f_shape_name.SetEnabled(shapeNames != null);
                f_shape_name.choices = shapeNames ?? new();

                f_shape_name.formatListItemCallback = name =>
                {
                    if (string.IsNullOrWhiteSpace(name)) return "";

                    if (shapeNames == null)
                    {
                        return $"<Missing SkinnedMeshRenderer>";
                    }
                    else if (!shapeNames.Contains(name))
                    {
                        return $"<color=\"red\">{name}</color>";
                    }
                    else
                    {
                        return name;
                    }
                };
                f_shape_name.formatSelectedValueCallback = f_shape_name.formatListItemCallback;
            }
        }
    }
}