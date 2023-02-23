using System;
using System.Linq;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuItemCoreGUI
    {
        internal SerializedObject _serializedObject;
        internal SerializedObject _gameObjects;

        internal SerializedProperty _name;
        internal SerializedProperty _control;
        internal SerializedProperty _type;
        internal SerializedProperty _parameter;
        internal SerializedProperty _value;

        internal ParameterGUI _parameterGUI;

        public MenuItemCoreGUI(SerializedObject _obj, Action redraw)
        {
            _serializedObject = _obj;
            _gameObjects = new SerializedObject(
                _obj.targetObjects.Select(o =>
                    (UnityEngine.Object) ((ModularAvatarMenuItem) o).gameObject
                ).ToArray()
            );

            _name = _gameObjects.FindProperty("m_Name");
            _control = _serializedObject.FindProperty(nameof(ModularAvatarMenuItem.Control));

            _type = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            _parameter = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            _value = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));

            _parameterGUI = new ParameterGUI(_parameter, redraw);
        }

        public void DoGUI()
        {
            EditorGUILayout.PropertyField(_name);
            EditorGUILayout.PropertyField(_type);
            EditorGUILayout.PropertyField(_value);

            _parameterGUI.DoGUI();
            EditorGUILayout.PropertyField(_value);
        }
    }
}