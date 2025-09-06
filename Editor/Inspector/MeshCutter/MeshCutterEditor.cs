#region

using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static nadena.dev.modular_avatar.core.editor.Localization;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(MeshCutterMultiMode))]
    internal class MeshCutterMultiModeDrawer : EnumDrawer<MeshCutterMultiMode>
    {
        protected override string localizationPrefix => "reactive_object.mesh_cutter.multi-mode";
    }
    
    [CustomEditor(typeof(ModularAvatarMeshCutter))]
    internal class MeshCutterEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

        protected override void OnInnerInspectorGUI()
        {
            DrawDefaultInspector();
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = uxml.CloneTree();
            UI.Localize(root);
            root.styleSheets.Add(uss);

            root.Bind(serializedObject);

            ROSimulatorButton.BindRefObject(root, target);

            var addVertexFilter = root.Q<DropdownField>("add-vertex-filter-dropdown");
            var choices = VertexFilterRegistry.ComponentLabels;
            var defaultLabel = "\u200B" + L.GetLocalizedString("reactive_object.mesh_cutter.add_vertex_filter");
            //choices.Insert(0, defaultLabel);
            addVertexFilter.choices = choices;
            addVertexFilter.SetValueWithoutNotify(defaultLabel);

            var hintField = root.Q<VisualElement>("hint-add-vertex-filter");
            addVertexFilter.UpdateWhileAttached(() =>
            {
                var shouldDisplayHint = false;

                if (targets.Length == 1)
                {
                    if (target is Component c)
                    {
                        shouldDisplayHint = !c.TryGetComponent<IVertexFilterBehavior>(out _);
                    }
                }

                hintField.style.display = shouldDisplayHint ? DisplayStyle.Flex : DisplayStyle.None;
            });

            addVertexFilter.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                if (VertexFilterRegistry.LabelToType.TryGetValue(evt.newValue, out var type))
                {
                    foreach (var target in targets)
                    {
                        if (target is ModularAvatarMeshCutter mc)
                        {
                            Undo.AddComponent(mc.gameObject, type);
                        }
                    }
                }

                addVertexFilter.SetValueWithoutNotify(defaultLabel);
            });

            return root;
        }
    }
}
