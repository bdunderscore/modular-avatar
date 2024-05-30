#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    public class AddShapePopup : PopupWindowContent
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/ShapeChanger/";
        const string UxmlPath = Root + "AddShapePopup.uxml";
        const string UssPath = Root + "ShapeChangerStyles.uss";

        private VisualElement _elem;
        private ScrollView _scrollView;

        public AddShapePopup(ModularAvatarShapeChanger changer)
        {
            if (changer == null) return;
            var target = changer.targetRenderer.Get(changer)?.GetComponent<SkinnedMeshRenderer>();
            if (target == null || target.sharedMesh == null) return;

            var alreadyRegistered = changer.Shapes.Select(c => c.ShapeName).ToHashSet();

            var keys = new List<string>();
            for (int i = 0; i < target.sharedMesh.blendShapeCount; i++)
            {
                var name = target.sharedMesh.GetBlendShapeName(i);
                if (alreadyRegistered.Contains(name)) continue;

                keys.Add(name);
            }

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

            _elem = uxml.CloneTree();
            _elem.styleSheets.Add(uss);
            Localization.UI.Localize(_elem);

            _scrollView = _elem.Q<ScrollView>("scroll-view");

            if (keys.Count > 0)
            {
                _scrollView.contentContainer.Clear();

                foreach (var key in keys)
                {
                    var container = new VisualElement();
                    container.AddToClassList("add-shape-row");

                    Button btn = default;
                    btn = new Button(() =>
                    {
                        AddShape(changer, key);
                        container.RemoveFromHierarchy();
                    });
                    btn.text = "+";
                    container.Add(btn);

                    var label = new Label(key);
                    container.Add(label);

                    _scrollView.contentContainer.Add(container);
                }
            }
        }

        private void AddShape(ModularAvatarShapeChanger changer, string key)
        {
            Undo.RecordObject(changer, "Add Shape");

            changer.Shapes.Add(new ChangedShape()
            {
                ShapeName = key,
                ChangeType = ShapeChangeType.Delete,
                Value = 100
            });
        }

        public override void OnGUI(Rect rect)
        {
        }

        public override void OnOpen()
        {
            editorWindow.rootVisualElement.Clear();
            editorWindow.rootVisualElement.Add(_elem);
            //editorWindow.rootVisualElement.Clear();

            //editorWindow.rootVisualElement.Add(new Label("Hello, World!"));
        }
    }
}