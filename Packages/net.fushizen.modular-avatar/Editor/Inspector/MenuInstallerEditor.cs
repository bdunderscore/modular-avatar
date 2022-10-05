using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuInstaller))]
    [CanEditMultipleObjects]
    public class MenuInstallerEditor : Editor
    {
        private ModularAvatarMenuInstaller _installer;
        private Editor _innerMenuEditor;
        private VRCExpressionsMenu _menuToAppend;

        private bool _menuFoldout;
        private bool _devFoldout;

        private void OnEnable()
        {
            _installer = (ModularAvatarMenuInstaller) target;
        }

        private void SetupMenuEditor()
        {
            if (targets.Length != 1)
            {
                _innerMenuEditor = null;
                _menuToAppend = null;
            }
            else if (_installer.menuToAppend != _menuToAppend)
            {
                if (_installer.menuToAppend == null) _innerMenuEditor = null;
                else
                {
                    _innerMenuEditor = CreateEditor(_installer.menuToAppend);
                }

                _menuToAppend = _installer.menuToAppend;
            }
        }

        public override void OnInspectorGUI()
        {
            SetupMenuEditor();

            var installTo = serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.installTargetMenu));
            EditorGUILayout.PropertyField(installTo, new GUIContent("Install To"));

            var avatar = RuntimeUtil.FindAvatarInParents(_installer.transform);
            if (avatar != null && GUILayout.Button("Select menu"))
            {
                AvMenuTreeViewWindow.Show(avatar, menu =>
                {
                    installTo.objectReferenceValue = menu;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            if (targets.Length == 1)
            {
                _menuFoldout = EditorGUILayout.Foldout(_menuFoldout, "Show menu contents");
                if (_menuFoldout)
                {
                    EditorGUI.indentLevel++;
                    using (var disabled = new EditorGUI.DisabledScope(true))
                    {
                        if (_innerMenuEditor != null) _innerMenuEditor.OnInspectorGUI();
                        else EditorGUILayout.HelpBox("No menu selected", MessageType.Info);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            _devFoldout = EditorGUILayout.Foldout(_devFoldout, "Developer Options");
            if (_devFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(ModularAvatarMenuInstaller.menuToAppend)),
                    new GUIContent("Menu to install")
                );
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}