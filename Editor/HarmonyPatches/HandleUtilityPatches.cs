#region

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class HandleUtilityPatches
    {
        internal static void Patch_FilterInstanceIDs(Harmony h)
        {
            var t_HandleUtility = AccessTools.TypeByName("UnityEditor.HandleUtility");
            var m_orig = AccessTools.Method(t_HandleUtility, "FilterInstanceIDs");

            var m_prefix = AccessTools.Method(typeof(HandleUtilityPatches), "Prefix_FilterInstanceIDs");

            h.Patch(original: m_orig, prefix: new HarmonyMethod(m_prefix));
        }

        [UsedImplicitly]
        private static bool Prefix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            out int[] parentInstanceIDs,
            out int[] childInstanceIDs
        )
        {
            gameObjects = RemapObjects(gameObjects);
            parentInstanceIDs = childInstanceIDs = null;
            return true;
        }

        private static IEnumerable<GameObject> RemapObjects(IEnumerable<GameObject> objs)
        {
            return objs.Select(
                obj =>
                {
                    if (obj == null) return obj;
                    if (ScaleAdjusterRenderer.originalObjects.TryGetValue(obj, out var proxy) && proxy != null)
                    {
                        return proxy.gameObject;
                    }
                    else
                    {
                        return obj;
                    }
                }
            ).ToArray();
        }
    }
}