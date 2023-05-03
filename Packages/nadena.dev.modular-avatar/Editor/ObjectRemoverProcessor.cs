using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public static class ObjectRemoverProcessor
    {
        internal static void OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var removers = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarObjectRemover>(true);
            foreach (var remover in removers)
            {
                foreach (var toRemove in remover.objectsToRemove) Object.DestroyImmediate(toRemove);
            }
        }
    }
}