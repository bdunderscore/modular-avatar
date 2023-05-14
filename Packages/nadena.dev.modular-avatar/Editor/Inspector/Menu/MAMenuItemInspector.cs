using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.menu;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMenuItem))]
    [CanEditMultipleObjects]
    internal class MAMenuItemInspector : MAEditorBase
    {
        private VRCExpressionsMenu MENU_CLOTHING;
        private ControlGroup CONTROL_GROUP_CLOTHING;

        private List<(GUIContent, Action)> _presets = new List<(GUIContent, Action)>();
        private MenuItemCoreGUI _coreGUI;

        private long _cacheSeq = -1;

        void OnEnable()
        {
            _coreGUI = new MenuItemCoreGUI(serializedObject, Repaint);
            _coreGUI.AlwaysExpandContents = true;

            MENU_CLOTHING = Util.LoadAssetByGuid<VRCExpressionsMenu>("2fe0aa7ecd6bc4443bade672c978f59d");
            CONTROL_GROUP_CLOTHING
                = Util.LoadAssetByGuid<GameObject>("e451e988456f35b49a3d011d780bda07")
                    ?.GetComponent<ControlGroup>();

            RebuildPresets();
        }

        private void RebuildPresets()
        {
            _presets.Clear();

            _cacheSeq = VirtualMenu.CacheSequence;

            if (targets.Length == 1)
            {
                ModularAvatarMenuItem item = (ModularAvatarMenuItem) target;
                if (item.GetComponent<ModularAvatarMenuInstaller>() == null
                    && item.GetComponent<MenuAction>() != null
                    && item.Control.type == VRCExpressionsMenu.Control.ControlType.Toggle
                    && item.controlGroup != CONTROL_GROUP_CLOTHING
                   )
                {
                    _presets.Add((new GUIContent("Configure as outfit toggle"), ConfigureOutfitToggle));
                }

                if (item.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && item.MenuSource == SubmenuSource.Children)
                {
                    _presets.Add((new GUIContent("Set items as exclusive toggles"), ConfigureExclusiveToggles));
                }
            }
        }

        private void ConfigureExclusiveToggles()
        {
            ModularAvatarMenuItem item = (ModularAvatarMenuItem) target;

            var controlGroup = item.gameObject.GetComponent<ControlGroup>();
            if (controlGroup == null)
            {
                controlGroup = Undo.AddComponent<ControlGroup>(item.gameObject);
            }

            var sourceObject = (item.menuSource_otherObjectChildren
                ? item.menuSource_otherObjectChildren
                : item.gameObject).transform;

            foreach (Transform t in sourceObject)
            {
                var subItem = t.GetComponent<ModularAvatarMenuItem>();
                if (subItem == null) continue;
                if (subItem.GetComponent<MenuAction>() == null) continue;

                Undo.RecordObject(subItem, "Configure exclusive toggles");
                subItem.controlGroup = controlGroup;
                subItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            }

            // Clear caches
            OnEnable();
        }

        private void ConfigureOutfitToggle()
        {
            ModularAvatarMenuItem item = (ModularAvatarMenuItem) target;
            Undo.RecordObject(item, "Configure outfit toggle");
            item.controlGroup = CONTROL_GROUP_CLOTHING;

            // Determine if the item is already registered in the virtual menu - if not, we need to install it
            var avatar = RuntimeUtil.FindAvatarInParents(item.transform);
            if (avatar != null)
            {
                VirtualMenu virtualMenu = VirtualMenu.ForAvatar(RuntimeUtil.FindAvatarInParents(item.transform));
                if (virtualMenu.ContainsNode(item)) return;
            }

            var installer = item.gameObject.AddComponent<ModularAvatarMenuInstaller>();
            Undo.RegisterCreatedObjectUndo(installer, "Configure outfit toggle");
            installer.installTargetMenu = MENU_CLOTHING;

            // Clear caches
            OnEnable();
        }

        protected override void OnInnerInspectorGUI()
        {
            if (VirtualMenu.CacheSequence != _cacheSeq) RebuildPresets();

            serializedObject.Update();

            _coreGUI.DoGUI();

            serializedObject.ApplyModifiedProperties();

            ShowPresetsButton();

            ShowLanguageUI();
        }

        private void ShowPresetsButton()
        {
            if (_presets.Count == 0) return;

            var style = EditorStyles.popup;
            var rect = EditorGUILayout.GetControlRect(true, 18f, style);
            var controlId = GUIUtility.GetControlID("MAPresetsButton".GetHashCode(), FocusType.Keyboard, rect);

            rect.xMin += 2 * rect.width / 3;

            if (GUI.Button(rect, new GUIContent("Presets"), EditorStyles.popup))
            {
                EditorUtility.DisplayCustomMenu(
                    rect,
                    _presets.Select(elem => elem.Item1).ToArray(),
                    -1,
                    (_userData, _options, _index) =>
                    {
                        if (_index >= 0 && _index < _presets.Count)
                        {
                            _presets[_index].Item2();

                            RebuildPresets();
                        }
                    },
                    controlId
                );
            }
        }
    }

    [CustomEditor(typeof(ModularAvatarMenuGroup))]
    internal class MAMenuGroupInspector : MAEditorBase
    {
        private MenuPreviewGUI _previewGUI;
        private SerializedProperty _prop_target;

        void OnEnable()
        {
            _previewGUI = new MenuPreviewGUI(Repaint);
            _prop_target = serializedObject.FindProperty(nameof(ModularAvatarMenuGroup.targetObject));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_prop_target, G("menuitem.prop.source_override"));

            _previewGUI.DoGUI((ModularAvatarMenuGroup) target);

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}