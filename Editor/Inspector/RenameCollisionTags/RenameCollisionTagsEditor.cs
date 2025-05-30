#if MA_VRCSDK3_AVATARS && UNITY_2022_1_OR_NEWER

#region

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using static nadena.dev.modular_avatar.core.editor.Localization;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
  [CustomEditor(typeof(ModularAvatarRenameVRChatCollisionTags))]
  internal class RenameCollisionTagsEditor : MAEditorBase
  {
    [SerializeField] private StyleSheet uss;
    [SerializeField] private VisualTreeAsset uxml;

    private ListView listView;

    protected override void OnInnerInspectorGUI()
    {
      EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
    }

    protected override VisualElement CreateInnerInspectorGUI()
    {
      var root = uxml.CloneTree();
      UI.Localize(root);
      root.styleSheets.Add(uss);

      // ListView を設定
      listView = root.Q<ListView>("CollisionTagsList");
      listView.showBoundCollectionSize = false;
      listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
      listView.selectionType = SelectionType.Multiple;

      root.Bind(serializedObject);

      return root;
    }
  }
}

#endif
