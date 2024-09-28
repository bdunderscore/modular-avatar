using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ToggleCreatorShortcut
    {
        [MenuItem(UnityMenuItems.GameObject_CreateToggle, false, UnityMenuItems.GameObject_CreateToggleOrder)]
        private static void CreateToggle()
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return;
            
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
            
            var toggle = new GameObject("New Toggle");
            
            var objToggle = toggle.AddComponent<ModularAvatarObjectToggle>();

            toggle.transform.SetParent(parent, false);

            var mami = toggle.AddComponent<ModularAvatarMenuItem>();
            mami.InitSettings();
            mami.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                name = "New Toggle",
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