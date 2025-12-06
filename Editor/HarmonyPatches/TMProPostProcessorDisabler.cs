using System;
using System.Linq;
using HarmonyLib;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    /// <summary>
    ///     The TMPro_TexturePostProcessor postprocessor attempts to load every texture that is ever
    ///     (re)imported. This causes a lot of overhead at times.
    ///     This postprocessor seems only to be relevant for certain updates to sprite assets, and would mostly
    ///     only affect worlds anyway, so just disable it.
    /// </summary>
    internal static class TMProPostProcessorDisabler
    {
        private static Type fontAsset;
        
        public static void Patch(Harmony h)
        {
            fontAsset = AccessTools.TypeByName("TMPro.TMP_FontAsset");
            
            var tpp = AccessTools.TypeByName("TMPro.EditorUtilities.TMPro_TexturePostProcessor");
            if (tpp == null) return;

            var method = AccessTools.Method(tpp, "OnPostprocessAllAssets");
            h.Patch(method, new HarmonyMethod(typeof(TMProPostProcessorDisabler), nameof(Prefix)));
        }

        private static bool Prefix(ref string[] importedAssets)
        {
            // Do reimport font assets at least (in case someone is actually using them)
            // (but exclude textures as there's going to be way too many of them)
            importedAssets = importedAssets.Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == fontAsset)
                .ToArray();

            return true;
        }
    }
}