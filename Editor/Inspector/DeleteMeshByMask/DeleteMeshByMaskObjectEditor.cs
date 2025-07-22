#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine;
#if MA_MASK_TEXTURE_EDITOR
using MaskTextureEditor = net.nekobako.MaskTextureEditor.Editor;
#endif

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(DeleteMeshByMaskObject))]
    public class DeleteMeshByMaskObjectEditor : PropertyDrawer
    {
#if MA_MASK_TEXTURE_EDITOR
        internal const string MaskTextureEditorToken = "nadena.dev.modular-avatar.delete-mesh-by-mask-editor";
        private static readonly Vector2Int DefaultTextureSize = new(1024, 1024);
#endif

        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/DeleteMeshByMask/";
        private const string UxmlPath = Root + "DeleteMeshByMaskObjectEditor.uxml";
        private const string UssPath = Root + "DeleteMeshByMaskStyles.uss";

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

#if MA_MASK_TEXTURE_EDITOR
            var f_mask_texture = uxml.Q<PropertyField>("f-mask-texture");
            var b_create_mask_texture = uxml.Q<Button>("b-create-mask-texture");
            var t_edit_mask_texture = uxml.Q<Toggle>("t-edit-mask-texture");

            var objectProperty = property.FindPropertyRelative(nameof(DeleteMeshByMaskObject.Object));
            var materialIndexProperty = property.FindPropertyRelative(nameof(DeleteMeshByMaskObject.MaterialIndex));
            var maskTextureProperty = property.FindPropertyRelative(nameof(DeleteMeshByMaskObject.MaskTexture));
            var deleteModeProperty = property.FindPropertyRelative(nameof(DeleteMeshByMaskObject.DeleteMode));

            f_object.RegisterValueChangeCallback(_ => UpdateButtonAndToggle());
            f_material_index.RegisterValueChangedCallback(_ => UpdateButtonAndToggle());
            f_mask_texture.RegisterValueChangeCallback(_ => UpdateButtonAndToggle());

            b_create_mask_texture.clicked += () =>
            {
                property.serializedObject.Update();

                var obj = AvatarObjectReference.Get(objectProperty);
                if (obj == null || !obj.TryGetComponent<Renderer>(out var renderer)) return;

                var slot = materialIndexProperty.intValue;
                if (slot >= renderer.sharedMaterials.Length) return;

                var texture = (DeleteMeshByMaskMode)deleteModeProperty.intValue switch
                {
                    DeleteMeshByMaskMode.DeleteBlack => MaskTextureEditor.Utility.CreateTexture(DefaultTextureSize, Color.white),
                    DeleteMeshByMaskMode.DeleteWhite => MaskTextureEditor.Utility.CreateTexture(DefaultTextureSize, Color.black),
                    _ => MaskTextureEditor.Utility.CreateTexture(DefaultTextureSize, Color.clear),
                };
                if (texture == null) return;

                maskTextureProperty.objectReferenceValue = texture;
                maskTextureProperty.serializedObject.ApplyModifiedProperties();

                MaskTextureEditor.Window.TryOpen(texture, renderer, slot, MaskTextureEditorToken);
                UpdateButtonAndToggle();
            };

            t_edit_mask_texture.RegisterValueChangedCallback(evt =>
            {
                property.serializedObject.Update();

                var obj = AvatarObjectReference.Get(objectProperty);
                if (obj == null || !obj.TryGetComponent<Renderer>(out var renderer)) return;

                var slot = materialIndexProperty.intValue;
                if (slot >= renderer.sharedMaterials.Length) return;

                var texture = maskTextureProperty.objectReferenceValue as Texture2D;
                if (texture == null) return;

                if (evt.newValue)
                {
                    MaskTextureEditor.Window.TryOpen(texture, renderer, slot, MaskTextureEditorToken);
                }
                else
                {
                    MaskTextureEditor.Window.TryClose();
                }
                UpdateButtonAndToggle();
            });

            uxml.RegisterCallback<AttachToPanelEvent>(_ => MaskTextureEditor.Window.IsOpen.OnChange += OnChangeIsOpenMaskTextureEditor);
            uxml.RegisterCallback<DetachFromPanelEvent>(_ => MaskTextureEditor.Window.IsOpen.OnChange -= OnChangeIsOpenMaskTextureEditor);

            UpdateButtonAndToggle();

            void OnChangeIsOpenMaskTextureEditor(bool _)
            {
                MaskTextureEditor.Window.IsOpen.OnChange -= OnChangeIsOpenMaskTextureEditor;
                MaskTextureEditor.Window.IsOpen.OnChange += OnChangeIsOpenMaskTextureEditor;
                UpdateButtonAndToggle();
            }

            void UpdateButtonAndToggle()
            {
                property.serializedObject.Update();

                var obj = AvatarObjectReference.Get(objectProperty);
                var renderer = obj == null ? null : obj.GetComponent<Renderer>();
                var slot = materialIndexProperty.intValue;
                var texture = maskTextureProperty.objectReferenceValue as Texture2D;

                var created = texture != null;
                b_create_mask_texture.style.display = created ? DisplayStyle.None : DisplayStyle.Flex;
                t_edit_mask_texture.style.display = created ? DisplayStyle.Flex : DisplayStyle.None;

                var editable = renderer != null && slot < renderer.sharedMaterials.Length;
                b_create_mask_texture.SetEnabled(editable);
                t_edit_mask_texture.SetEnabled(editable);

                var editing = editable && MaskTextureEditor.Window.IsOpenFor(texture, renderer, slot, MaskTextureEditorToken);
                t_edit_mask_texture.SetValueWithoutNotify(editing);
                uxml.Query<VisualElement>(null, "disable-while-editing-mask-texture").ForEach(x => x.SetEnabled(!editing));
            }
#else
            var b_create_mask_texture = uxml.Q<Button>("b-create-mask-texture");
            var t_edit_mask_texture = uxml.Q<Toggle>("t-edit-mask-texture");

            b_create_mask_texture.style.display = DisplayStyle.None;
            t_edit_mask_texture.style.display = DisplayStyle.None;
#endif

            return uxml;
        }
    }
}
