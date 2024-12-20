﻿#if MA_VRCSDK3_AVATARS
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ToggleCreatorShortcut
    {
        [MenuItem(UnityMenuItems.GameObject_SetupToggle, false, UnityMenuItems.GameObject_SetupToggleOrder)]
        private static void SetupToggle() => CreateToggles(true);

        [MenuItem(UnityMenuItems.GameObject_CreateToggle, false, UnityMenuItems.GameObject_CreateToggleOrder)]
        private static void CreateToggle() => CreateToggles(false);
        
        private static void CreateToggles(bool setup)
        {
            var selections = Selection.objects;
            foreach (var selection in selections) {
                if (selection == null) continue;
                CreateToggleImpl(selection as GameObject, setup);
            }
            Selection.objects = null;
        }

        private static void CreateToggleImpl(GameObject selected, bool setup)
        {
            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selected.transform);
            if (avatarRoot == null) return;

            bool createInstaller = true;
            Transform parent = avatarRoot;

            try
            {
                var selectedMenuItem = selected.GetComponent<ModularAvatarMenuItem>();
                if (selectedMenuItem?.Control?.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && selectedMenuItem.MenuSource == SubmenuSource.Children
                   )
                {
                    parent = selected.transform;
                    createInstaller = false;
                }
            }
            catch (MissingComponentException)
            {
                // ignore
            }

            var name = setup ? selected.name + " Toggle"  : "New Toggle";
            
            var toggle = new GameObject(name);
            
            var objToggle = toggle.AddComponent<ModularAvatarObjectToggle>();
            if (setup)
            {
                var path = RuntimeUtil.RelativePath(avatarRoot.gameObject, selected);
                objToggle.Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(){ referencePath = path },
                    Active = false
                });
            }


            toggle.transform.SetParent(parent, false);

            var mami = toggle.AddComponent<ModularAvatarMenuItem>();
            mami.InitSettings();
            mami.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                name = name,
                value = 1,
            };
            
            if (createInstaller)
            {
                toggle.AddComponent<ModularAvatarMenuInstaller>();
            }

            Selection.activeGameObject = toggle;
            EditorGUIUtility.PingObject(objToggle);

            
            Undo.RegisterCreatedObjectUndo(toggle, "Create Toggle");
        }
    }
}
#endif