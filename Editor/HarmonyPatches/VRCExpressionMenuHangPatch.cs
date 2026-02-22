#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using HarmonyLib;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    // Workaround for https://feedback.vrchat.com/sdk-bug-reports/p/sdk-3101-pathologically-slow-behavior-in-vrcexpressionsmenu-issubmenurecursive-c
    //
    // VRCExpressionsMenuEditor uses IsSubmenuRecursive to check whether a menu is a submenu of the avatar root menu
    // in OnEnable, with the intent of setting the "parameters" field if it is. However, this traversal does not deal
    // with having multiple paths to the same menu, and has only very limited protection from loops (in that there is
    // an "arbitrary" depth limit of 16). This can result in editor hangs when there are many paths to the same node.
    //
    // Since there's no point visiting the same node multiple times for the same needle menu, we cache whether we've
    // visited the menu before here. It's theoretically possible for this to give a different result, since the second
    // visit might have a different depth, but I'll leave it up to VRC to do the "correct" fix, and just prevent the
    // editor hang for now.
    internal static class VRCExpressionMenuHangPatch
    {
        private static readonly HashSet<VRCExpressionsMenu> _visited = new();

        public static void Patch(Harmony obj)
        {
            var ty_editor = AccessTools.TypeByName("VRCExpressionsMenuEditor");
            if (ty_editor == null) return;

            var onEnable = AccessTools.Method(ty_editor, "OnEnable");
            var isSubmenuRecursive = AccessTools.Method(ty_editor, "IsSubmenuRecursive");

            if (onEnable == null || isSubmenuRecursive == null) return;

            obj.Patch(onEnable,
                // Clear first to ensure we start with a clean slate
                new HarmonyMethod(typeof(VRCExpressionMenuHangPatch), nameof(ClearVisited)),
                // Clear after to avoid keeping references to objects longer than necessary (potential memory leak)
                new HarmonyMethod(typeof(VRCExpressionMenuHangPatch), nameof(ClearVisited))
            );
            obj.Patch(isSubmenuRecursive,
                new HarmonyMethod(typeof(VRCExpressionMenuHangPatch), nameof(IsSubmenuRecursivePrefix)));
        }

        private static void ClearVisited()
        {
            _visited.Clear();
        }

        private static bool IsSubmenuRecursivePrefix(
            VRCExpressionsMenu needle,
            VRCExpressionsMenu haystack,
            int depth,
            ref bool __result
        )
        {
            if (haystack != null && !_visited.Add(haystack))
            {
                // Already visited
                __result = false;
                return false;
            }

            return true;
        }
    }
}

#endif