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

            var f_object = uxml.Q<ObjectField>("f-object");
            f_object.objectType = typeof(SkinnedMeshRenderer);
            f_object.allowSceneObjects = true;

            var f_target_object = uxml.Q<ObjectField>("f-obj-target-object");
            var f_reference_path = uxml.Q<TextField>("f-obj-ref-path");

            f_object.RegisterValueChangedCallback(evt =>
            {
                var gameObj = (evt.newValue as SkinnedMeshRenderer)?.gameObject;

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

                EditorApplication.delayCall += UpdateShapeDropdown;
            });
            UpdateShapeDropdown();

            f_target_object.RegisterValueChangedCallback(_ => UpdateVisualTarget());
            f_reference_path.RegisterValueChangedCallback(_ => UpdateVisualTarget());

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

            void UpdateVisualTarget()
            {
                var changer = property.serializedObject.targetObject as ModularAvatarShapeChanger;
                var renderer = GetTargetRenderer(AvatarObjectReference.Get(property.FindPropertyRelative("Object")));
                var overrideRenderer = GetTargetRenderer(changer?.targetRenderer.Get(changer));

                f_object.SetEnabled(overrideRenderer == null);
                f_object.SetValueWithoutNotify(overrideRenderer ?? renderer);

                SkinnedMeshRenderer GetTargetRenderer(GameObject obj)
                {
                    try
                    {
                        return obj?.GetComponent<SkinnedMeshRenderer>();
                    }
                    catch (MissingComponentException e)
                    {
                        return null;
                    }
                }
            }

            void UpdateShapeDropdown()
            {
                var changer = property.serializedObject.targetObject as ModularAvatarShapeChanger;
                var shapeNames = GetShapeNames(AvatarObjectReference.Get(property.FindPropertyRelative("Object")));
                var overrideShapeNames = GetShapeNames(changer?.targetRenderer.Get(changer));

                f_shape_name.SetEnabled(overrideShapeNames != null || shapeNames != null);
                f_shape_name.choices = overrideShapeNames ?? shapeNames ?? new();

                f_shape_name.formatListItemCallback = name => f_shape_name.enabledSelf ? name : "<Missing SkinnedMeshRenderer>";
                f_shape_name.formatSelectedValueCallback = f_shape_name.formatListItemCallback;

                List<string> GetShapeNames(GameObject obj)
                {
                    try
                    {
                        var mesh = obj?.GetComponent<SkinnedMeshRenderer>()?.sharedMesh;
                        if (mesh == null) return null;

                        return Enumerable.Range(0, mesh.blendShapeCount)
                            .Select(x => mesh.GetBlendShapeName(x))
                            .ToList();
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