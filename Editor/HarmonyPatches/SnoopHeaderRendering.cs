#region

using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    /// <summary>
    /// ScaleAdjusterRenderer toggles off the enabled state of the original mesh just before rendering,
    /// in order to allow us to effectively replace it at rendering time. We restore this in OnPostRender,
    /// but GUI rendering can happen before this; as such, snoop GUI events and re-enable the original
    /// at that time.
    /// </summary>
    internal class SnoopHeaderRendering
    {
        internal static void Patch(Harmony harmony)
        {
            var t_orig = AccessTools.TypeByName("UnityEditor.UIElements.EditorElement");
            var m_orig = AccessTools.Method(t_orig, "HeaderOnGUI");

            var m_prefix = AccessTools.Method(typeof(SnoopHeaderRendering), "Prefix");

            harmony.Patch(original: m_orig, prefix: new HarmonyMethod(m_prefix));

            var t_GUIUtility = typeof(GUIUtility);
            var m_ProcessEvent = AccessTools.Method(t_GUIUtility, "ProcessEvent");

            harmony.Patch(original: m_ProcessEvent, prefix: new HarmonyMethod(m_prefix));
        }

        [UsedImplicitly]
        private static void Prefix()
        {
            ScaleAdjusterRenderer.ClearAllOverrides();
        }
    }
}