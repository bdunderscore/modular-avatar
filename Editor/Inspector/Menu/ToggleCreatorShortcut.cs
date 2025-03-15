#if MA_VRCSDK3_AVATARS
using System.Linq;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ToggleCreatorShortcut
    {
        [MenuItem(UnityMenuItems.GameObject_CreateToggleForSelection, false, UnityMenuItems.GameObject_CreateToggleForSelectionOrder)]
        private static void CreateToggleForSelection()
        {
            var forSelection = true;

            var selections = Selection.objects.OfType<GameObject>();
            // Ignore GameObjects with submenu in the context of CreateToggleForSelection.
            selections = selections.Where(s => !TryGetChildrenSourceSubmenu(s, out var _));
            if (selections.Count() == 0) return;

            foreach (var selected in selections)
            {
                var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selected.transform);
                if (avatarRoot == null) return;

                var parent = selected.transform.parent?.gameObject;
                if (parent == null) continue;

                CreateToggleImpl(selected, parent, selected.name + " Toggle", forSelection, createInstaller:true);
            }

            Selection.objects = null;
        }

        [MenuItem(UnityMenuItems.GameObject_CreateToggle, false, UnityMenuItems.GameObject_CreateToggleOrder)]
        private static void CreateToggle()
        {
            var selections = Selection.objects.OfType<GameObject>();
            if (selections.Count() == 0) return;

            foreach (var selected in selections)
            {
                var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selected.transform);
                if (avatarRoot == null) return;

                var parent = avatarRoot.gameObject;
                var createInstaller = true;

                if (TryGetChildrenSourceSubmenu(selected, out var _))
                {
                    parent = selected;
                    createInstaller = false;
                }

                CreateToggleImpl(selected, parent, "New Toggle", createInstaller:createInstaller);
            }

            Selection.objects = null;
        }
        
        private static bool TryGetChildrenSourceSubmenu(GameObject target, out ModularAvatarMenuItem subMenu)
        {
            subMenu = null;
            try
            {
                var mami = target.GetComponent<ModularAvatarMenuItem>();
                if (mami?.Control?.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && mami.MenuSource == SubmenuSource.Children
                   )
                {
                    subMenu = mami;
                    return true;
                }
            }
            catch (MissingComponentException)
            {
                // ignore
            }
            return false;
        }

        private static void CreateToggleImpl(GameObject selected, GameObject parent, string name, bool forSelection = false, bool createInstaller = true)
        {
            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selected.transform);
            if (avatarRoot == null) return;
            
            var toggle = new GameObject(name);
            
            var objToggle = toggle.AddComponent<ModularAvatarObjectToggle>();
            if (forSelection)
            {
                var path = RuntimeUtil.RelativePath(avatarRoot.gameObject, selected);
                objToggle.Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(){ referencePath = path },
                    Active = !selected.activeSelf
                });
            }


            toggle.transform.SetParent(parent.transform, false);

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