#region

using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(VertexFilterByMaskComponent))]
    internal class VFByMaskEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshCutter/";
        private const string UxmlPath = Root + "VFByMaskEditor.uxml";
        private const string UssPath = Root + "MeshCutterStyles.uss";

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.Bind(serializedObject);

            var f_object = uxml.Q<PropertyField>("f-cutter-object");

            // Locate any Mesh Cutter on the same object as this VFByMaskComponent, and use its object field as a
            // reference
            var singleTarget = (VertexFilterByMaskComponent)serializedObject.targetObject;
            ModularAvatarMeshCutter? cutter = null;
            if (singleTarget != null && singleTarget.TryGetComponent(out cutter))
            {
                f_object.Bind(new SerializedObject(cutter));
            }

            Func<Renderer?> getRenderer = () =>
            {
                if (cutter == null) return null;
                if (cutter.Object.Get(cutter)?.TryGetComponent<Renderer>(out var result) == true)
                {
                    return result;
                }

                return null;
            };
            
            var f_material_index = uxml.Q<IntegerField>("f-material-index");
            var f_material_index_dropdown = uxml.Q<DropdownField>("f-material-index-dropdown");
            var f_material_index_original = uxml.Q<ObjectField>("f-material-index-original");
            MaterialSlotSelector.Setup(getRenderer, f_object, f_material_index, f_material_index_dropdown,
                f_material_index_original);

#if MA_MASK_TEXTURE_EDITOR
            var f_mask_texture = uxml.Q<PropertyField>("f-mask-texture");
            f_mask_texture.parent.Add(new MaskTextureEditorOpener(
                serializedObject, f_object, f_material_index, f_mask_texture,
                editing => uxml.Query<VisualElement>(null, "disable-while-editing-mask-texture").ForEach(x => x.SetEnabled(!editing))));
            uxml.AddToClassList("has-mask-editor");
#endif

            uxml.Q<Button>("b-install-mask-editor").clickable.clicked += () =>
            {
                Application.OpenURL("https://vpm.nekobako.net/");
            };

            return uxml;
        }

        protected override void OnInnerInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}