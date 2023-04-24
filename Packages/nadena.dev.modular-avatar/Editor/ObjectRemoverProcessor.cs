using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public static class ObjectRemoverProcessor
    {
        internal static void OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var toRemove = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarObjectRemover>(true);

            foreach (var remove in toRemove)
            {
                Object.DestroyImmediate(remove.gameObject);
            }
        }


        [MenuItem("GameObject/ModularAvatar/Restore Removed", false, 49)]
        static void RestoreRemoved(MenuCommand menuCommand)
        {
            if (!(menuCommand.context is GameObject obj)) return;
            foreach (Transform child in obj.transform)
            {
                var toRemove = child.GetComponent<ModularAvatarObjectRemover>();
                if (toRemove != null) toRemove.Restore();
            }
        }
    }
}