using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class ExportPrereleaseWarning
    {
        private static Harmony _harmony;

        public static void Patch(Harmony h)
        {
            // Performing class initialization of PackageExport too early breaks its gui styles.
            // Avoid this by deferring this hook.
            _harmony = h;
            #if !UNITY_6000_2_OR_NEWER // this callback has changed on unity 6000.5, we'll lean on the fallback for now
            EditorApplication.hierarchyWindowItemOnGUI += DoDeferredPatch;
            #endif

            // In case the hierarchy is not displayed, render after 10 editor frames, by which point
            // _hopefully_ the GUI is initialized
            EditorApplication.update += DeferredPatchCountdown;
        }

        private static bool deferredPatchComplete;
        private static int countdown = 10;

        private static void DeferredPatchCountdown()
        {
            if (--countdown == 0) DoDeferredPatch();
        }
        
        private static void DoDeferredPatch(int ignored, Rect ignored2)
        {
            DoDeferredPatch();
        }

        private static void DoDeferredPatch()
        {
            // We can't touch PackageExport until the gui style is loaded, so we wait for the hierarchy
            // window OnGUI event, by which point things should be set up.
            if (deferredPatchComplete) return;
            deferredPatchComplete = true;

            EditorApplication.delayCall += () =>
            {
#if !UNITY_6000_2_OR_NEWER
                EditorApplication.hierarchyWindowItemOnGUI -= DoDeferredPatch;
#endif
                EditorApplication.update -= DeferredPatchCountdown;
            };

            var packageExport = AccessTools.TypeByName("UnityEditor.PackageExport");
            var tva = AccessTools.Method(packageExport, "TreeViewArea");

            _harmony.Patch(tva,
                new HarmonyMethod(typeof(ExportPrereleaseWarning), nameof(before_TreeViewArea)));
        }

        private static void before_TreeViewArea()
        {
            if (VersionTag.Current.IsPrerelease)
            {
                EditorGUILayout.HelpBox(Localization.S("version_mismatch.export_warning"), MessageType.Warning);
            }
        }
    }
}