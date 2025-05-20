#region

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
  [CustomEditor(typeof(ModularAvatarRenameCollisionTags))]
  [CanEditMultipleObjects]
  internal class RenameCollisionTagsEditor : MAEditorBase
  {
    private ReorderableList _list;
    private SerializedProperty _collisionTags;

    protected override void OnInnerInspectorGUI()
    {
      serializedObject.Update();

      _list.DoLayoutList();

      ShowLanguageUI();

      serializedObject.ApplyModifiedProperties();
    }

    private void OnEnable()
    {
      InitList();
    }

    private void InitList()
    {
      _collisionTags = serializedObject.FindProperty(nameof(ModularAvatarRenameCollisionTags.configs));
      _list = new ReorderableList(serializedObject,
          _collisionTags,
          true, false, true, true
      )
      {
        drawElementCallback = DrawElementCallback
      };
      _list.elementHeight += 2;
    }
    private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
    {
      rect.height = EditorGUIUtility.singleLineHeight;

      var collisionTag = _collisionTags.GetArrayElementAtIndex(index);
      var tagName = collisionTag.FindPropertyRelative(nameof(RenameCollisionTagConfig.name));

      EditorGUI.PropertyField(rect, tagName, new GUIContent(G("rename_collision_tags.label")));
    }
  }
}
