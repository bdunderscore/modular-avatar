using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuItemCoreGUI
    {
        private readonly SerializedProperty _name;
        private readonly SerializedProperty _texture;
        private readonly SerializedProperty _type;
        private readonly SerializedProperty _value;
        private readonly SerializedProperty _submenu;

        private readonly ParameterGUI _parameterGUI;

        public MenuItemCoreGUI(SerializedObject _obj, Action redraw)
        {
            GameObject parameterReference = null;
            if (_obj.targetObjects.Length == 1)
            {
                parameterReference = (_obj.targetObject as Component)?.gameObject;
            }

            var gameObjects = new SerializedObject(
                _obj.targetObjects.Select(o =>
                    (UnityEngine.Object) ((ModularAvatarMenuItem) o).gameObject
                ).ToArray()
            );

            _name = gameObjects.FindProperty("m_Name");
            var control = _obj.FindProperty(nameof(ModularAvatarMenuItem.Control));

            _texture = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.icon));
            _type = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            var parameter = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            _value = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));
            _submenu = null;

            _parameterGUI = new ParameterGUI(parameterReference, parameter, redraw);
        }

        public MenuItemCoreGUI(GameObject parameterReference, SerializedProperty _control, Action redraw)
        {
            _name = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.name));
            _texture = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.icon));
            _type = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            var parameter = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            _value = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));
            _submenu = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subMenu));

            _parameterGUI = new ParameterGUI(parameterReference, parameter, redraw);
        }

        public void DoGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.PropertyField(_name);
            EditorGUILayout.PropertyField(_texture);
            EditorGUILayout.PropertyField(_type);
            EditorGUILayout.PropertyField(_value);

            _parameterGUI.DoGUI();

            EditorGUILayout.EndVertical();

            if (_texture != null)
            {
                var tex = _texture.objectReferenceValue as Texture2D;
                if (tex != null)
                {
                    var size = EditorGUIUtility.singleLineHeight * 5;
                    var margin = 4;
                    var withMargin = new Vector2(margin + size, margin + size);

                    var rect = GUILayoutUtility.GetRect(withMargin.x, withMargin.y, GUILayout.ExpandWidth(false),
                        GUILayout.ExpandHeight(true));
                    rect.x += margin;
                    rect.y = rect.y + rect.height / 2 - size / 2;
                    rect.width = size;
                    rect.height = size;

                    GUI.Box(rect, new GUIContent(), "flow node 1");
                    GUI.DrawTexture(rect, tex);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_submenu != null) EditorGUILayout.PropertyField(_submenu);
        }
    }
}