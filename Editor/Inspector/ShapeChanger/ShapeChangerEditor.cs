#region

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static System.Reflection.BindingFlags;
using PopupWindow = UnityEditor.PopupWindow;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarShapeChanger))]
    public class ShapeChangerEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

        private DragAndDropManipulator _dragAndDropManipulator;
        private BlendshapeSelectWindow _window;

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

            _dragAndDropManipulator = new DragAndDropManipulator(root.Q("group-box"), target as ModularAvatarShapeChanger);

            // The Add button callback isn't exposed publicly for some reason...
            var field_addButton = typeof(BaseListView).GetField("m_AddButton", NonPublic | Instance);
            var addButton = (Button)field_addButton.GetValue(listView);

            addButton.clickable = new Clickable(OpenAddWindow);

            return root;
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as ModularAvatarShapeChanger;
        }

        private class DragAndDropManipulator : DragAndDropManipulator<ModularAvatarShapeChanger>
        {
            public DragAndDropManipulator(VisualElement targetElement, ModularAvatarShapeChanger targetComponent)
                : base(targetElement, targetComponent) { }

            protected override bool FilterGameObject(GameObject obj)
            {
                if (obj.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                {
                    return smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0;
                }
                return false;
            }

            protected override void AddObjectReferences(AvatarObjectReference[] references)
            {
                Undo.RecordObject(TargetComponent, "Add Changed Shapes");

                foreach (var reference in references)
                {
                    var changedShape = new ChangedShape { Object = reference, ShapeName = string.Empty };
                    TargetComponent.Shapes.Add(changedShape);
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }

        private void OnDisable()
        {
            if (_window != null) DestroyImmediate(_window);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_window != null) DestroyImmediate(_window);
        }

        private void OpenAddWindow()
        {
            if (_window != null) DestroyImmediate(_window);
            _window = CreateInstance<BlendshapeSelectWindow>();
            _window.AvatarRoot = RuntimeUtil.FindAvatarTransformInParents((target as ModularAvatarShapeChanger).transform).gameObject;
            _window.OfferBinding += OfferBinding;
            _window.Show();
        }

        private void OfferBinding(BlendshapeBinding binding)
        {
            var changer = target as ModularAvatarShapeChanger;
            if (changer.Shapes.Any(x => x.Object.Equals(binding.ReferenceMesh) && x.ShapeName == binding.Blendshape))
            {
                return;
            }

            Undo.RecordObject(changer, "Add Shape");

            changer.Shapes.Add(new ChangedShape()
            {
                Object = binding.ReferenceMesh,
                ShapeName = binding.Blendshape,
                ChangeType = ShapeChangeType.Delete,
                Value = 100
            });

            PrefabUtility.RecordPrefabInstancePropertyModifications(changer);
        }
    }
}