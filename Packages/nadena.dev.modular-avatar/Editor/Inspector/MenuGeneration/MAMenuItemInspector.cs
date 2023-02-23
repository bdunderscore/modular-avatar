using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuItem))]
    internal class MAMenuItemInspector : MAEditorBase
    {
        private SerializedProperty prop_submenu_source;
        private SerializedProperty prop_control;
        private SerializedProperty prop_otherObjChildren;

        void OnEnable()
        {
            prop_control = serializedObject.FindProperty(nameof(ModularAvatarMenuItem.Control));
            prop_submenu_source = serializedObject.FindProperty(nameof(ModularAvatarMenuItem.MenuSource));
            prop_otherObjChildren =
                serializedObject.FindProperty(nameof(ModularAvatarMenuItem.menuSource_otherObjectChildren));
        }

        private void DrawControlSettings(SerializedProperty control, string name = null,
            Action<string> commitName = null)
        {
            if (name != null)
            {
                EditorGUI.BeginChangeCheck();
                var newName = EditorGUILayout.TextField("Name", name);
                if (EditorGUI.EndChangeCheck() && commitName != null)
                {
                    commitName(newName);
                }
            }

            var prop_type = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            var prop_parameter = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            var prop_value = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));

            EditorGUILayout.PropertyField(prop_type);
            EditorGUILayout.PropertyField(prop_parameter, new GUIContent("Parameter"));
            EditorGUILayout.PropertyField(prop_value);
        }

        protected override void OnInnerInspectorGUI()
        {
            bool multiEdit = targets.Length != 1;
            string name = null;
            Action<string> commitName = null;
            if (!multiEdit)
            {
                EditorGUI.BeginChangeCheck();
                var targetGameObject = ((ModularAvatarMenuItem) target).gameObject;
                name = targetGameObject.name;
                commitName = newName =>
                {
                    Undo.RecordObject(targetGameObject, "Rename object");
                    targetGameObject.name = newName;
                };
            }

            serializedObject.Update();

            DrawControlSettings(prop_control, name, commitName);

            serializedObject.ApplyModifiedProperties();

            if (multiEdit) return;

            var menuItem = (ModularAvatarMenuItem) target;
            if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                GUILayout.Space(EditorStyles.label.lineHeight);
                EditorGUILayout.LabelField("Sub Menu", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(prop_submenu_source);

                if (prop_submenu_source.enumValueIndex == (int) SubmenuSource.Children)
                {
                    EditorGUILayout.PropertyField(prop_otherObjChildren);
                }

                serializedObject.ApplyModifiedProperties();

                switch (menuItem.MenuSource)
                {
                    default: break;
                    case SubmenuSource.Children:
                    {
                        var source = menuItem.menuSource_otherObjectChildren != null
                            ? menuItem.menuSource_otherObjectChildren
                            : menuItem.gameObject;
                        foreach (Transform t in source.transform)
                        {
                            var child = t.GetComponent<ModularAvatarMenuItem>();
                            if (child == null) continue;

                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                            GUILayout.BeginHorizontal();
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.ObjectField(new GUIContent(), child, typeof(ModularAvatarMenuItem),
                                    true,
                                    GUILayout.ExpandWidth(true));
                            }

                            GUILayout.Space(20);
                            GUILayout.Label("Enabled", GUILayout.Width(50));
                            var childObject = t.gameObject;
                            EditorGUI.BeginChangeCheck();
                            var active = GUILayout.Toggle(childObject.activeSelf, new GUIContent(),
                                GUILayout.Width(EditorGUIUtility.singleLineHeight));
                            if (EditorGUI.EndChangeCheck())
                            {
                                childObject.SetActive(active);
                            }

                            GUILayout.EndHorizontal();

                            name = t.gameObject.name;
                            commitName = newName =>
                            {
                                Undo.RecordObject(t.gameObject, "Rename object");
                                t.gameObject.name = newName;
                            };

                            var childSO = new SerializedObject(child);
                            var childControl = childSO.FindProperty(nameof(ModularAvatarMenuItem.Control));
                            DrawControlSettings(childControl, name, commitName);
                            childSO.ApplyModifiedProperties();

                            EditorGUILayout.EndVertical();
                        }

                        break;
                    }
                }
            }
        }
    }
}