#region

using System;
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


        protected override void OnInnerInspectorGUI()
        {
            throw new NotImplementedException();
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = uxml.CloneTree();
            Localization.UI.Localize(root);
            root.styleSheets.Add(uss);

            root.Bind(serializedObject);

            var listView = root.Q<ListView>("Shapes");

            listView.showBoundCollectionSize = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            // The Add button callback isn't exposed publicly for some reason...
            var field_addButton = typeof(BaseListView).GetField("m_AddButton", NonPublic | Instance);
            var addButton = (Button)field_addButton.GetValue(listView);

            addButton.clickable = new Clickable(() =>
            {
                PopupWindow.Show(addButton.worldBound, new AddShapePopup(target as ModularAvatarShapeChanger));
            });

            return root;
        }
    }
}