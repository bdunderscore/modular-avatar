#nullable enable

using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(VertexFilterByBoneComponent))]
    internal class VFByBoneEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshCutter/";
        private const string UxmlPath = Root + "VFByBoneEditor.uxml";
        private const string UssPath = Root + "MeshCutterStyles.uss";

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.Bind(serializedObject);

            RestrictedEnumDropdown.Bind(
                uxml.Q<DropdownField>("f-selection-mode"),
                serializedObject.FindProperty(nameof(VertexFilterByBoneComponent.m_selectionMode)),
                new[] { VertexSelectionMode.AnyVertex, VertexSelectionMode.AllVertices },
                "reactive_object.delete-mesh.selection-mode"
            );

            return uxml;
        }

        protected override void OnInnerInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
