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
            SnoopHeaderRendering.Patch1,
            SnoopHeaderRendering.Patch2,
            HideScaleAdjusterFromPrefabOverrideView.Patch
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
        }
    }
}