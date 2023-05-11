using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class AddOutfitMenu
    {
        private static readonly GameObject PREFAB_OUTFIT_MENU =
            Util.LoadAssetByGuid<GameObject>("2097228906bdff44fb40a3d9f39cf599");

        private static readonly string PATH_OUTFIT_MENU = AssetDatabase.GetAssetPath(PREFAB_OUTFIT_MENU);

        [MenuItem("GameObject/ModularAvatar/Add outfit menu", false, 49)]
        internal static void CommandAddOutfitMenu(MenuCommand menuCommand)
        {
            if (!(menuCommand.context is GameObject gameObj)) return;
            var avatar = gameObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null || avatar.expressionsMenu == null) return;

            // Do we have a top-level menu we can install into?
            // For our heuristic purposes we only look at menus at the top level of the avatar, where the avatar's own
            // menu is empty.
            GameObject topLevelMenu = null;
            if (avatar.expressionsMenu?.controls?.Count == 0)
            {
                foreach (Transform t in avatar.transform)
                {
                    var installer = t.GetComponent<ModularAvatarMenuInstaller>();
                    var group = t.GetComponent<ModularAvatarMenuGroup>();

                    if (installer != null && group != null && installer.installTargetMenu == null &&
                        group.targetObject == null)
                    {
                        topLevelMenu = t.gameObject;
                        break;
                    }
                }
            }

            var parentObject = topLevelMenu != null ? topLevelMenu.transform : avatar.transform;

            // Avoid double registration
            foreach (Transform t in parentObject.transform)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(t) &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t) == PATH_OUTFIT_MENU)
                {
                    EditorGUIUtility.PingObject(t.gameObject);
                    return;
                }
            }

            var instance = (GameObject) PrefabUtility.InstantiatePrefab(PREFAB_OUTFIT_MENU, parentObject);
            instance.name = "Outfits";
            Undo.RegisterCreatedObjectUndo(instance, "Added outfit menu");

            var installerComponent = instance.GetComponent<ModularAvatarMenuInstaller>();
            if (topLevelMenu != null)
            {
                UnityEngine.Object.DestroyImmediate(installerComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            }

            EditorGUIUtility.PingObject(instance.transform.Find("BaseOutfit").gameObject);
        }
    }
}