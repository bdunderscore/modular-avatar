using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMoveTo))]
    [CanEditMultipleObjects]
    internal class MoveToEditor : MAEditorBase
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MoveTo/";
        private const string UxmlPath = Root + "MoveToEditor.uxml";

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            Localization.UI.Localize(root);
            root.Bind(serializedObject);
            return root;
        }

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }
    }
}