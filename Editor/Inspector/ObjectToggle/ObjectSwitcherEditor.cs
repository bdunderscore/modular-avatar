#region

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarObjectToggle))]
    public class ObjectSwitcherEditor : MAEditorBase
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

            _dragAndDropManipulator = new DragAndDropManipulator(root.Q("group-box"), target as ModularAvatarObjectToggle);

            return root;
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as ModularAvatarObjectToggle;
        }

        private class DragAndDropManipulator : DragAndDropManipulator<ModularAvatarObjectToggle>
        {
            public DragAndDropManipulator(VisualElement targetElement, ModularAvatarObjectToggle targetComponent)
                : base(targetElement, targetComponent) { }

            protected override bool AllowKnownObjects => false;

            protected override void AddObjectReferences(AvatarObjectReference[] references)
            {
                Undo.RecordObject(TargetComponent, "Add Toggled Objects");

                foreach (var reference in references)
                {
                    var toggledObject = new ToggledObject { Object = reference, Active = !reference.Get(TargetComponent).activeSelf };
                    TargetComponent.Objects.Add(toggledObject);
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }
    }
}