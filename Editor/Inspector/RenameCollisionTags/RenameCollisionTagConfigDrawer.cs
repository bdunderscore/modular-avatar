#if MA_VRCSDK3_AVATARS && UNITY_2022_1_OR_NEWER

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor.RenameCollisionTags
{
  [CustomPropertyDrawer(typeof(RenameCollisionTagConfig))]
  internal class RenameCollisionTagConfigDrawer : PropertyDrawer
  {
    private const string ScriptGUID = "407de235b1606634db6f47aab47e9bcd";
    
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
      var scriptPath = AssetDatabase.GUIDToAssetPath(ScriptGUID);
      var rootPath = Path.GetDirectoryName(scriptPath);
      var ussPath = Path.Combine(rootPath, "RenameCollisionTagsEditor.uss").Replace("\\", "/");
      var uxmlPath = Path.Combine(rootPath, "RenameCollisionTagConfigDrawer.uxml").Replace("\\", "/");

      var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
      var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

      var root = uxml.CloneTree();
      Localization.UI.Localize(root);
      root.styleSheets.Add(uss);

      var autoRename = root.Q<Toggle>("f-auto-rename");
      autoRename.RegisterValueChangedCallback(evt =>
      {
        if (evt.newValue)
          root.AddToClassList("st-auto-rename");
        else
          root.RemoveFromClassList("st-auto-rename");
      });

      root.Q<VisualElement>("rename-to-group-disabled").SetEnabled(false);


      var name = root.Q<TextField>("f-name");
      var renameTo = root.Q<TextField>("f-rename-to");
      var renameToInnerElement = renameTo.Q<TextElement>();
      var renameToPlaceholder = root.Q<Label>("f-rename-to-placeholder");
      renameToPlaceholder.pickingMode = PickingMode.Ignore;

      Action updateRenameToPlaceholder = () =>
      {
        if (string.IsNullOrWhiteSpace(renameTo.value))
          renameToPlaceholder.text = name.value;
        else
          renameToPlaceholder.text = "";
      };

      name.RegisterValueChangedCallback(evt => { updateRenameToPlaceholder(); });
      renameTo.RegisterValueChangedCallback(evt => { updateRenameToPlaceholder(); });

      renameToPlaceholder.RemoveFromHierarchy();
      renameToInnerElement.Add(renameToPlaceholder);
      updateRenameToPlaceholder();

      // Prevent delete keypresses from bubbling up when text fields are focused
      foreach (var elem in root.Query<TextElement>().Build())
      {
        elem.RegisterCallback<KeyDownEvent>(evt =>
        {
          if (evt.keyCode == KeyCode.Delete && evt.modifiers == EventModifiers.FunctionKey)
            evt.StopPropagation();
        });
      }

      return root;
    }
  }
}

#endif
