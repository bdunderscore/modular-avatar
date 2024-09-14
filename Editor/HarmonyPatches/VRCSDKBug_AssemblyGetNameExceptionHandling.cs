using HarmonyLib;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class VRCSDKBug_AssemblyGetNameExceptionHandling
    {
        internal static void Patch(Harmony h)
        {
            var t_Tools = AccessTools.TypeByName("VRC.Tools");
            var p_HasTypeVRCApplication = AccessTools.Property(t_Tools, "HasTypeVRCApplication");

            h.Patch(p_HasTypeVRCApplication.GetMethod,
                new HarmonyMethod(typeof(VRCSDKBug_AssemblyGetNameExceptionHandling), nameof(AlwaysFalse)));
        }

        private static bool AlwaysFalse(ref bool __result)
        {
            __result = false;

            return false;
        }
    }
}