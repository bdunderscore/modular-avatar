using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarReactiveMove))]
    [CanEditMultipleObjects]
    internal class ReactiveMoveEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/ReactiveMove/";
        private const string UxmlPath = Root + "ReactiveMoveEditor.uxml";
        private const string UssPath = Root + "ReactiveMoveEditor.uss";

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath));

            Localization.UI.Localize(root);
            root.Bind(serializedObject);
            ROSimulatorButton.BindRefObject(root, target);

            var fixToWorld = root.Q<PropertyField>("fix-to-world");
            var whereTo = root.Q<PropertyField>("where-to");

            fixToWorld.RegisterValueChangeCallback(evt => { whereTo.SetEnabled(!evt.changedProperty.boolValue); });
            
            return root;
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }
    }
}