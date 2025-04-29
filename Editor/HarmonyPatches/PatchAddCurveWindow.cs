using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class PatchAddCurveWindow
    {
        internal static void Patch(Harmony h)
        {
            var ty = AccessTools.TypeByName("UnityEditorInternal.AddCurvesPopupHierarchyDataSource");
            if (ty == null)
            {
                Debug.LogWarning("Failed to find AddCurvesPopupHierarchyDataSource type");
                return;
            }

            var method = AccessTools.Method(ty, "AddGameObjectToHierarchy");
            if (method == null)
            {
                Debug.LogWarning("Failed to find AddGameObjectToHierarchy method");
                return;
            }
            
            h.Patch(method, transpiler: new HarmonyMethod(typeof(PatchAddCurveWindow), nameof(Transpile)));
        }


        // Transpiler for AddCurvesPopupHierarchyDataSource
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var target =
                typeof(AnimationUtility).GetMethod("GetAnimatableBindings", BindingFlags.Static | BindingFlags.Public);
            var replacement = AccessTools.Method(typeof(PatchAddCurveWindow), nameof(GetAnimatableBindings));

            foreach (var insn in instructions)
            {
                if (insn.Is(OpCodes.Call, target))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else
                {
                    yield return insn;
                }
            }
        }
        
        private static EditorCurveBinding[] GetAnimatableBindings(GameObject targetObject, GameObject rootObject)
        {
            var bindings = AnimationUtility.GetAnimatableBindings(targetObject, rootObject);

            if (bindings.Length == 0)
            {
                // Try again with the object as a root; this workarounds issues where unity misidentifies the animator root
                // when multiple similar armatures are in an avatar (and, for whatever reason, this causes GetAnimatableBindings to
                // return an empty array)
                return AnimationUtility.GetAnimatableBindings(targetObject, targetObject);
            }
            else
            {
                return bindings;
            }
        }
    }
}