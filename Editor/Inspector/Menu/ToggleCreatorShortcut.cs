#if MA_VRCSDK3_AVATARS
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ToggleCreatorShortcut
    {
        [MenuItem(UnityMenuItems.GameObject_CreateToggleForSelection, false, UnityMenuItems.GameObject_CreateToggleForSelectionOrder)]
        private static void CreateToggleForSelection(MenuCommand command) => CreateToggleImpl(command, true);

        [MenuItem(UnityMenuItems.GameObject_CreateToggle, false, UnityMenuItems.GameObject_CreateToggleOrder)]
        private static void CreateToggle(MenuCommand command) => CreateToggleImpl(command, false);
        
        private static void CreateToggleImpl(MenuCommand command, bool forSelection)
        {
            var selected = command.context as GameObject;
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

            var name = forSelection ? selected.name + " Toggle"  : "New Toggle";
            
            var toggle = new GameObject(name);
            
            var objToggle = toggle.AddComponent<ModularAvatarObjectToggle>();
            if (forSelection)
            {
                var path = RuntimeUtil.RelativePath(avatarRoot.gameObject, selected);
                objToggle.Inverted = selected.activeSelf;
                objToggle.Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(){ referencePath = path },
                    Active = !selected.activeSelf
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
            if (forSelection)
            {
                mami.isDefault = selected.activeSelf;
            }
            
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