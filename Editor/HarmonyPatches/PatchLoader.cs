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
        private static readonly Action<Harmony>[] patches = new Action<Harmony>[]
        {
            VRCSDKBug_AssemblyGetNameExceptionHandling.Patch,
            HierarchyViewPatches.Patch,
            #if UNITY_2022_3_OR_NEWER
            HandleUtilityPatches.Patch_FilterInstanceIDs,
            PickingObjectPatch.Patch,
            #endif
        };

        [InitializeOnLoadMethod]
        static void ApplyPatches()
        {
            var harmony = new Harmony("nadena.dev.modular_avatar");

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

            AssemblyReloadEvents.beforeAssemblyReload += () => { harmony.UnpatchAll(); };
        }
    }
}