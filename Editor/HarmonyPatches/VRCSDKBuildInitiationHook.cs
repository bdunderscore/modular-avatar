#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using nadena.dev.modular_avatar.core.editor.SyncParameterSequence;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.HarmonyPatches
{
    internal static class VRCSDKBuildInitiationHook
    {
        public static void Patch(Harmony harmony)
        {
            var methodInfo = AccessTools.TypeByName("VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder")
                                 ?.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?.Where(m => m.Name == "Build")
                             ?? Array.Empty<MethodInfo>();

            foreach (var method in methodInfo)
            {
                if (method.GetParameters().FirstOrDefault()?.ParameterType == typeof(GameObject))
                {
                    harmony.Patch(
                        method,
                        new HarmonyMethod(typeof(VRCSDKBuildInitiationHook), nameof(BeforeBuild))
                    );
                }
            }
        }

        private static bool BeforeBuild(GameObject target)
        {
            SyncParameterSequencePass.LastPrimaryTarget.Value = target;
            return true;
        }
    }
}

#endif