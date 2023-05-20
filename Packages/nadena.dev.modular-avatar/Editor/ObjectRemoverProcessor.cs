using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public static class ObjectRemoverProcessor
    {
        internal static void OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var removers = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarObjectRemover>(true);
            var objectsToRemove = new List<GameObject>();
            foreach (var remover in removers)
            {
                objectsToRemove.AddRange(remover.objectsToRemove);
            }

            foreach (var toRemove in objectsToRemove) Object.DestroyImmediate(toRemove);
        }
    }
}