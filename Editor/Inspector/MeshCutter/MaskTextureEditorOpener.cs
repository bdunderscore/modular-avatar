#if MA_MASK_TEXTURE_EDITOR
using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using MaskTextureEditor = net.nekobako.MaskTextureEditor.Editor;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MaskTextureEditorOpener : VisualElement
    {
        internal const string MaskTextureEditorToken = "nadena.dev.modular-avatar.delete-mesh-by-mask-editor";
        private static readonly Vector2Int DefaultTextureSize = new(1024, 1024);

        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshCutter/";
        private const string UxmlPath = Root + "MaskTextureEditorOpener.uxml";
        private const string UssPath = Root + "MeshCutterStyles.uss";

        public MaskTextureEditorOpener(SerializedObject serializedObj, PropertyField f_object,
            IntegerField f_material_index, PropertyField f_mask_texture,
            Action<bool> onChangeEditing)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);

            var b_create_mask_texture = uxml.Q<Button>("b-create-mask-texture");
            var t_edit_mask_texture = uxml.Q<Toggle>("t-edit-mask-texture");

            var materialIndexProperty = serializedObj.FindProperty(nameof(VertexFilterByMaskComponent.m_materialIndex));
            var maskTextureProperty = serializedObj.FindProperty(nameof(VertexFilterByMaskComponent.m_maskTexture));
            var deleteModeProperty = serializedObj.FindProperty(nameof(VertexFilterByMaskComponent.m_deleteMode));

            f_object.RegisterValueChangeCallback(_ => UpdateButtonAndToggle());
            f_material_index.RegisterValueChangedCallback(_ => UpdateButtonAndToggle());
            f_mask_texture.RegisterValueChangeCallback(_ => UpdateButtonAndToggle());

            b_create_mask_texture.clicked += () =>
            {
                serializedObj.Update();

                if (!TryGetRenderer(serializedObj, out var renderer)) return;

                var slot = materialIndexProperty.intValue;
                if (slot >= renderer.sharedMaterials.Length) return;

                var texture = (ByMaskMode)deleteModeProperty.intValue switch
                {
                    ByMaskMode.DeleteBlack => MaskTextureEditor.Utility.CreateTexture(DefaultTextureSize, Color.white),
                    ByMaskMode.DeleteWhite => MaskTextureEditor.Utility.CreateTexture(DefaultTextureSize, Color.black),
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
                serializedObj.Update();

                if (!TryGetRenderer(serializedObj, out var renderer)) return;

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

            Add(uxml);

            void OnChangeIsOpenMaskTextureEditor(bool _)
            {
                MaskTextureEditor.Window.IsOpen.OnChange -= OnChangeIsOpenMaskTextureEditor;
                MaskTextureEditor.Window.IsOpen.OnChange += OnChangeIsOpenMaskTextureEditor;
                UpdateButtonAndToggle();
            }

            void UpdateButtonAndToggle()
            {
                serializedObj.Update();

                TryGetRenderer(serializedObj, out var renderer);
                
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
                onChangeEditing(editing);
            }
        }

        private static bool TryGetRenderer(SerializedObject property, out Renderer renderer)
        {
            var singleTarget = (Component)property.targetObject;
            if (singleTarget == null || !singleTarget.TryGetComponent<ModularAvatarMeshCutter>(out var cutter))
            {
                renderer = null;
                return false;
            }

            var obj = cutter.Object.Get(cutter);
            if (obj == null || !obj.TryGetComponent(out renderer))
            {
                renderer = null;
                return false;
            }

            return true;
        }
    }
}
#endif
