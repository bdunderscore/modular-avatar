#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(VertexFilterByShapeComponent))]
    internal class VFByShapeEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshCutter/";
        private const string UxmlPath = Root + "VFByShapeEditor.uxml";
        private const string UssPath = Root + "MeshCutterStyles.uss";

        // Initialized in OnEnable() before CreateInnerInspectorGUI() is called
        private SerializedProperty _shapes = null!;
        private SerializedProperty _threshold = null!;
        
        private void OnEnable()
        {
            _shapes = serializedObject.FindProperty(nameof(VertexFilterByShapeComponent.m_shapes));
            _threshold = serializedObject.FindProperty(nameof(VertexFilterByShapeComponent.m_threshold));
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.Bind(serializedObject);

            var listView = uxml.Q<ListView>("Shapes");
            listView.showBoundCollectionSize = false;
            var b_addItem = listView.Q<Button>(BaseListView.footerAddButtonName);

            // Recreate the add item button to clear the clickable event
            var b_addItemNew = new Button();
            b_addItemNew.name = b_addItem.name;
            b_addItemNew.text = b_addItem.text;
            b_addItem.parent.Insert(b_addItem.parent.IndexOf(b_addItem), b_addItemNew);
            b_addItem.RemoveFromHierarchy();

            b_addItemNew.clickable.clicked += () =>
            {
                var targetMesh = GetTargetMesh();

                if (targetMesh == null)
                {
                    // Just add an empty entry
                    _shapes.arraySize++;
                    return;
                }

                int? addedIndex = null;
                var window = CreateInstance<BlendshapeSelectWindow>();
                window.AvatarRoot = RuntimeUtil.FindAvatarTransformInParents((target as Component)?.transform).gameObject;
                window.SingleMesh = targetMesh;
                window.OfferBinding = (binding) =>
                {
                    if (binding.Blendshape != null)
                    {
                        serializedObject.Update();
                        if (addedIndex == null)
                        {
                            addedIndex = _shapes.arraySize++;
                        }

                        _shapes.GetArrayElementAtIndex(addedIndex.Value).stringValue = binding.Blendshape;
                        serializedObject.ApplyModifiedProperties();
                    }

                    window.Close();
                };
                window.OfferSingleClick = (binding) =>
                {
                    if (binding.Blendshape != null)
                    {
                        serializedObject.Update();
                        if (addedIndex == null)
                        {
                            addedIndex = _shapes.arraySize++;
                        }

                        _shapes.GetArrayElementAtIndex(addedIndex.Value).stringValue = binding.Blendshape;
                        serializedObject.ApplyModifiedProperties();
                    }
                };
                window.OfferMultipleBindings = (bindings) =>
                {
                    serializedObject.Update();
                    foreach (var binding in bindings)
                    {
                        if (binding.Blendshape == null) continue;

                        if (addedIndex == null)
                        {
                            addedIndex = _shapes.arraySize++;
                        }
                        else
                        {
                            _shapes.arraySize++;
                        }

                        _shapes.GetArrayElementAtIndex(_shapes.arraySize - 1).stringValue = binding.Blendshape;
                    }
                    serializedObject.ApplyModifiedProperties();
                    window.Close();
                };
                window.Show();
            };

            return uxml;
        }

        private Mesh? GetTargetMesh()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            if (target is not VertexFilterByShapeComponent component) return null;
            if (!component.TryGetComponent<ModularAvatarMeshCutter>(out var meshCutter)) return null;
            var targetRendererObject = meshCutter.Object.Get(meshCutter);
            if (targetRendererObject == null ||
                !targetRendererObject.TryGetComponent<SkinnedMeshRenderer>(out var smr)) return null;
            return smr.sharedMesh;
        }

        protected override void OnInnerInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}