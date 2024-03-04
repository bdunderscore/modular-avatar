#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor.SceneManagement;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    /// <summary>
    /// Try to prevent various internal objects from showing up in the Prefab Overrides window...
    /// </summary>
    internal class HideScaleAdjusterFromPrefabOverrideView
    {
        internal static Type t_PrefabOverrides;
        internal static PropertyInfo p_AddedGameObjects, p_ObjectOverrides;

        internal static void Patch(Harmony harmony)
        {
            var t_PrefabOverridesTreeView = AccessTools.TypeByName("UnityEditor.PrefabOverridesTreeView");
            var m_GetPrefabOverrides = AccessTools.Method(t_PrefabOverridesTreeView, "GetPrefabOverrides");

            var m_postfix = AccessTools.Method(typeof(HideScaleAdjusterFromPrefabOverrideView), "Postfix");

            t_PrefabOverrides = AccessTools.TypeByName("UnityEditor.PrefabOverridesTreeView+PrefabOverrides");
            p_AddedGameObjects = AccessTools.Property(t_PrefabOverrides, "addedGameObjects");
            p_ObjectOverrides = AccessTools.Property(t_PrefabOverrides, "objectOverrides");

            harmony.Patch(original: m_GetPrefabOverrides, postfix: new HarmonyMethod(m_postfix));
        }

        [UsedImplicitly]
        private static void Postfix(GameObject prefabInstance, object __result)
        {
            List<AddedGameObject> added = p_AddedGameObjects.GetValue(__result) as List<AddedGameObject>;

            if (added == null) return;
            added.RemoveAll(obj => ScaleAdjusterRenderer.proxyObjects.ContainsKey(obj.instanceGameObject));

            List<ObjectOverride> objectOverrides = p_ObjectOverrides.GetValue(__result) as List<ObjectOverride>;
            if (objectOverrides == null) return;
            objectOverrides.RemoveAll(oo =>
            {
                var c = oo.instanceObject as Component;
                return c != null && ScaleAdjusterRenderer.proxyObjects.ContainsKey(c.gameObject);
            });
        }
    }
}