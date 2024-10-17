#region

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarMaterialSetter))]
    public class MaterialSetterEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

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

            var listView = root.Q<ListView>("Shapes");

            listView.showBoundCollectionSize = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            _dragAndDropManipulator = new DragAndDropManipulator(root.Q("group-box"), target as ModularAvatarMaterialSetter);

            return root;
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as ModularAvatarMaterialSetter;
        }

        private class DragAndDropManipulator : DragAndDropManipulator<ModularAvatarMaterialSetter>
        {
            public DragAndDropManipulator(VisualElement targetElement, ModularAvatarMaterialSetter targetComponent)
                : base(targetElement, targetComponent) { }

            protected override bool FilterGameObject(GameObject obj)
            {
                if (obj.TryGetComponent<Renderer>(out var renderer))
                {
                    return renderer.sharedMaterials.Length > 0;
                }
                return false;
            }

            protected override void AddObjectReferences(AvatarObjectReference[] references)
            {
                Undo.RecordObject(TargetComponent, "Add Material Switch Objects");

                foreach (var reference in references)
                {
                    var materialSwitchObject = new MaterialSwitchObject { Object = reference, MaterialIndex = 0 };
                    TargetComponent.Objects.Add(materialSwitchObject);
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }
    }
}