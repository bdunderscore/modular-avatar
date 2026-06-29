#nullable enable

using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(VertexFilterByUVTileComponent))]
    internal class VFByUVTileEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MeshCutter/";
        private const string UxmlPath = Root + "VFByUVTileEditor.uxml";
        private const string UssPath = Root + "MeshCutterStyles.uss";

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            uxml.Bind(serializedObject);

            SetupBound(uxml, "f-use-u-min", "f-u-min-op", "m_uMinInclusive");
            SetupBound(uxml, "f-use-u-max", "f-u-max-op", "m_uMaxInclusive");
            SetupBound(uxml, "f-use-v-min", "f-v-min-op", "m_vMinInclusive");
            SetupBound(uxml, "f-use-v-max", "f-v-max-op", "m_vMaxInclusive");

            return uxml;
        }

        private void SetupBound(VisualElement root, string toggleName, string opName, string propName)
        {
            var toggle = root.Q<Toggle>(toggleName);
            var op = root.Q<DropdownField>(opName);
            var valueName = opName.Substring(0, opName.Length - 3);
            var value = root.Q<FloatField>(valueName);

            var prop = serializedObject.FindProperty(propName);
            op.index = prop.boolValue ? 1 : 0;
            op.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                prop.boolValue = op.index == 1;
                serializedObject.ApplyModifiedProperties();
            });

            void UpdateEnabled()
            {
                var enabled = toggle.value;
                op.SetEnabled(enabled);
                value.SetEnabled(enabled);

                op.EnableInClassList("uvtile-disabled", !enabled);
                value.EnableInClassList("uvtile-disabled", !enabled);
            }

            UpdateEnabled();
            toggle.RegisterValueChangedCallback(_ => UpdateEnabled());
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }
    }
}