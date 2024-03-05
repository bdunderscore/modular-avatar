#region

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class HierarchyViewPatches
    {
        private static readonly Type t_HierarchyProperty = AccessTools.TypeByName("UnityEditor.HierarchyProperty");
        private static readonly PropertyInfo p_pptrValue = AccessTools.Property(t_HierarchyProperty, "pptrValue");

        private static FieldInfo f_m_Rows; // List<TreeViewItem>
        private static FieldInfo f_m_RowCount; // int
        private static PropertyInfo p_objectPPTR;

        internal static void Patch(Harmony h)
        {
#if MODULAR_AVATAR_DEBUG_HIDDEN
            return;
#endif
            var t_GameObjectTreeViewDataSource = AccessTools.TypeByName("UnityEditor.GameObjectTreeViewDataSource");
            var t_GameObjectTreeViewItem = AccessTools.TypeByName("UnityEditor.GameObjectTreeViewItem");

            f_m_Rows = t_GameObjectTreeViewDataSource.GetField("m_Rows",
                BindingFlags.NonPublic | BindingFlags.Instance);
            f_m_RowCount =
                t_GameObjectTreeViewDataSource.GetField("m_RowCount", BindingFlags.NonPublic | BindingFlags.Instance);
            p_objectPPTR = t_GameObjectTreeViewItem.GetProperty("objectPPTR");

            var m_orig = AccessTools.Method(t_GameObjectTreeViewDataSource, "InitTreeViewItem",
                new[]
                {
                    t_GameObjectTreeViewItem,
                    typeof(int),
                    typeof(Scene),
                    typeof(bool),
                    typeof(int),
                    typeof(Object),
                    typeof(bool),
                    typeof(int)
                });
            var m_patch = AccessTools.Method(typeof(HierarchyViewPatches), "Prefix_InitTreeViewItem");

            h.Patch(original: m_orig, prefix: new HarmonyMethod(m_patch));

            var m_InitRows = AccessTools.Method(t_GameObjectTreeViewDataSource, "InitializeRows");
            var m_transpiler = AccessTools.Method(typeof(HierarchyViewPatches), "Transpile_InitializeRows");

            h.Patch(original: m_InitRows,
                transpiler: new HarmonyMethod(m_transpiler),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HierarchyViewPatches), "Postfix_InitializeRows")),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(HierarchyViewPatches), "Prefix_InitializeRows"))
            );
        }

        private static int skipped = 0;

        private static void Prefix_InitializeRows()
        {
            skipped = 0;
        }

        [UsedImplicitly]
        private static void Postfix_InitializeRows(object __instance)
        {
            var rows = (IList<TreeViewItem>)f_m_Rows.GetValue(__instance);

            var rowCount = (int)f_m_RowCount.GetValue(__instance);

            f_m_RowCount.SetValue(__instance, rowCount - skipped);

            for (int i = 0; i < skipped; i++)
            {
                rows.RemoveAt(rows.Count - 1);
            }
        }

        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpile_InitializeRows(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            foreach (var c in Transpile_InitializeRows0(instructions, generator))
            {
                //Debug.Log(c);
                yield return c;
            }
        }

        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpile_InitializeRows0(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var m_shouldLoop = AccessTools.Method(typeof(HierarchyViewPatches), "ShouldLoop");

            var m_Next = AccessTools.Method(t_HierarchyProperty, "Next", new[] { typeof(int[]) });

            foreach (var c in instructions)
            {
                if (c.Is(OpCodes.Callvirt, m_Next))
                {
                    var loopLabel = generator.DefineLabel();
                    var stash_arg = generator.DeclareLocal(typeof(int[]));
                    var stash_obj = generator.DeclareLocal(t_HierarchyProperty);

                    yield return new CodeInstruction(OpCodes.Stloc, stash_arg);
                    yield return new CodeInstruction(OpCodes.Stloc, stash_obj);

                    var tmp = new CodeInstruction(OpCodes.Ldloc, stash_obj);
                    tmp.labels.Add(loopLabel);
                    yield return tmp;

                    yield return new CodeInstruction(OpCodes.Ldloc, stash_arg);
                    yield return new CodeInstruction(OpCodes.Call, m_Next);

                    // Check if this item should be ignored.
                    yield return new CodeInstruction(OpCodes.Ldloc, stash_obj);
                    yield return new CodeInstruction(OpCodes.Call, m_shouldLoop);
                    yield return new CodeInstruction(OpCodes.Brtrue_S, loopLabel);
                }
                else
                {
                    yield return c;
                }
            }
        }

        [UsedImplicitly]
        private static bool ShouldLoop(object hierarchyProperty)
        {
            if (hierarchyProperty == null) return false;

            var pptrValue = p_pptrValue.GetValue(hierarchyProperty);
            if (pptrValue == null) return false;

            var skip = ProxyManager.ProxyToOriginalObject.ContainsKey((GameObject)pptrValue);
            if (skip) skipped++;

            return skip;
        }

        private static bool Prefix_InitTreeViewItem(
            object __instance,
            ref object item,
            int itemID,
            Scene scene,
            bool isSceneHeader,
            int colorCode,
            Object pptrObject,
            ref bool hasChildren,
            int depth
        )
        {
            if (pptrObject == null || isSceneHeader) return true;

            if (hasChildren && ProxyManager.ProxyToOriginalObject.ContainsKey((GameObject)pptrObject))
            {
                // See if there are any other children...
                hasChildren = ((GameObject)pptrObject).transform.childCount > 1;
            }

            return true;
        }
    }
}