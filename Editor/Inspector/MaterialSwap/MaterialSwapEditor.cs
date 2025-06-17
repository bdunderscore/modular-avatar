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
    [CustomEditor(typeof(ModularAvatarMaterialSwap))]
    public class MaterialSwapEditor : MAEditorBase
    {
        [SerializeField]
        private StyleSheet uss;

        [SerializeField]
        private VisualTreeAsset uxml;

        private DragAndDropManipulator _dragAndDropManipulator;

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = uxml.CloneTree();
            Localization.UI.Localize(root);
            root.styleSheets.Add(uss);

            root.Bind(serializedObject);

            ROSimulatorButton.BindRefObject(root, target);

            var listView = root.Q<ListView>("Swaps");

            listView.showBoundCollectionSize = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            _dragAndDropManipulator = new DragAndDropManipulator(root.Q("group-box"), target as ModularAvatarMaterialSwap);

            return root;
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as ModularAvatarMaterialSwap;
        }

        private class DragAndDropManipulator : DragAndDropManipulator<ModularAvatarMaterialSwap, Material>
        {
            public DragAndDropManipulator(VisualElement targetElement, ModularAvatarMaterialSwap targetComponent)
                : base(targetElement, targetComponent)
            {
            }

            protected override IEnumerable<Material> FilterObjects(IEnumerable<Material> materials)
            {
                return materials.Where(x => TargetComponent.Swaps.All(y => x != y.From));
            }

            protected override void AddObjects(IEnumerable<Material> materials)
            {
                Undo.RecordObject(TargetComponent, "Add MatSwap");

                foreach (var material in materials)
                {
                    TargetComponent.Swaps.Add(new() { From = material });
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }
    }
}
