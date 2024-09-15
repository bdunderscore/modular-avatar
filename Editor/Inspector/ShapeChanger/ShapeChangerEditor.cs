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

            // The Add button callback isn't exposed publicly for some reason...
            var field_addButton = typeof(BaseListView).GetField("m_AddButton", NonPublic | Instance);
            var addButton = (Button)field_addButton.GetValue(listView);

            addButton.clickable = new Clickable(OpenAddWindow);

            return root;
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