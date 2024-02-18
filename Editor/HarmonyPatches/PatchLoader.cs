#region

using HarmonyLib;
using UnityEditor;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal class PatchLoader
    {
        [InitializeOnLoadMethod]
        static void ApplyPatches()
        {
            var harmony = new Harmony("nadena.dev.modular_avatar");

            SnoopHeaderRendering.Patch(harmony);
            HideScaleAdjusterFromPrefabOverrideView.Patch(harmony);
        }
    }
}