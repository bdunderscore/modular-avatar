using HarmonyLib;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    /// <summary>
    ///     The TMPro_TexturePostProcessor postprocessor attempts to load every texture that is ever
    ///     (re)imported. This causes a lot of overhead at times.
    ///     This postprocessor seems only to be relevant for certain updates to sprite assets, and would mostly
    ///     only affect worlds anyway, so just disable it.
    /// </summary>
    internal class TMProPostProcessorDisabler
    {
        public static void Patch(Harmony h)
        {
            var tpp = AccessTools.TypeByName("TMPro.EditorUtilities.TMPro_TexturePostProcessor");
            if (tpp == null) return;

            var method = AccessTools.Method(tpp, "OnPostprocessAllAssets");
            h.Patch(method, new HarmonyMethod(typeof(TMProPostProcessorDisabler), "Prefix"));
        }

        private static bool Prefix()
        {
            return false;
        }
    }
}