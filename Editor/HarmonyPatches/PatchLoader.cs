#region

using System;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal class PatchLoader
    {
        private const string HarmonyId = "nadena.dev.modular_avatar";

        private static readonly Action<Harmony>[] patches = new Action<Harmony>[]
        {
            //HierarchyViewPatches.Patch,
        };

        [InitializeOnLoadMethod]
        static void ApplyPatches()
        {
            var harmony = new Harmony(HarmonyId);

            foreach (var patch in patches)
            {
                try
                {
                    patch(harmony);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            AssemblyReloadEvents.beforeAssemblyReload += () => { harmony.UnpatchAll(HarmonyId); };
        }
    }
}