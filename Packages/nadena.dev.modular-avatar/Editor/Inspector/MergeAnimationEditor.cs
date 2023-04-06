using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(MergeAnimatorPathMode))]
    class PathModeDrawer : EnumDrawer<MergeAnimatorPathMode>
    {
        protected override string localizationPrefix => "path_mode";
    }

    [CustomEditor(typeof(ModularAvatarMergeAnimator))]
    class MergeAnimationEditor : MAEditorBase
    {
        private SerializedProperty prop_animators,
            prop_deleteAttachedAnimator,
            prop_pathMode,
            prop_matchAvatarWriteDefaults;

        private ReorderableList _list;

        private void OnEnable()
        {
            InitList();
            prop_deleteAttachedAnimator =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.deleteAttachedAnimator));
            prop_pathMode = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.pathMode));
            prop_matchAvatarWriteDefaults =
                serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.matchAvatarWriteDefaults));
        }

        private void InitList()
        {
            prop_animators = serializedObject.FindProperty(nameof(ModularAvatarMergeAnimator.animators));
            _list = new ReorderableList(serializedObject,
                prop_animators,
                true, true, true, true
            );
            _list.drawHeaderCallback = DrawHeader;
            _list.drawElementCallback = DrawElement;
            _list.elementHeight += 2;
        }

        private float elementWidth = 0;

        private void ComputeRects(
            Rect rect,
            out Rect targetLayerRect,
            out Rect animatorRect
)
        {
            if (elementWidth > 1 && elementWidth < rect.width)
            {
                rect.x += rect.width - elementWidth;
                rect.width = elementWidth;
            }

            targetLayerRect = rect;
            targetLayerRect.width /= 2;

            animatorRect = rect;
            animatorRect.width /= 2;
            animatorRect.x = targetLayerRect.x + targetLayerRect.width;

            targetLayerRect.width -= 12;
            animatorRect.width -= 12;
        }

        private void DrawHeader(Rect rect)
        {
            ComputeRects(rect, out var targetLayerRect, out var animatorRect);

            EditorGUI.LabelField(targetLayerRect, G("merge_animator.layer_type"));
            EditorGUI.LabelField(animatorRect, G("merge_animator.animator"));
        }

        private void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            rect.height -= 2;
            rect.y += 1;

            if (Math.Abs(elementWidth - rect.width) > 0.5f && rect.width > 1)
            {
                elementWidth = rect.width;
                Repaint();
            }

            ComputeRects(rect, out var targetLayerRect, out var animatorRect);

            var item = prop_animators.GetArrayElementAtIndex(index);
            var type = item.FindPropertyRelative(nameof(AnimLayerData.type));
            var animator = item.FindPropertyRelative(nameof(AnimLayerData.animator));

            using (var scope = new ZeroIndentScope())
            {
                EditorGUI.PropertyField(targetLayerRect, type, GUIContent.none);
                EditorGUI.PropertyField(animatorRect, animator, GUIContent.none);
            }
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            _list.DoLayoutList();
            EditorGUILayout.PropertyField(prop_deleteAttachedAnimator, G("merge_animator.delete_attached_animator"));
            EditorGUILayout.PropertyField(prop_pathMode, G("merge_animator.path_mode"));
            EditorGUILayout.PropertyField(prop_matchAvatarWriteDefaults,
                G("merge_animator.match_avatar_write_defaults"));

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}