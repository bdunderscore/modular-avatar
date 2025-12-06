#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Linq;
using System.Threading;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.vrchat;
using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.ModularAvatarSyncParameterSequence;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class SyncParameterSequencePass : Pass<SyncParameterSequencePass>
    {
        // This should really be on the Harmony patch side, but due to them being in different assemblies,
        // it's hard to reference in that direction (we'd end up with circular dependencies unless we
        // also move out PluginDefinition to its own asmdef).
        internal static AsyncLocal<GameObject?> LastPrimaryTarget = new();
        
        internal static BuildTarget? OverrideBuildTarget;
        
        private static Platform? CurrentPlatform
        {
            get
            {
                switch (OverrideBuildTarget ?? EditorUserBuildSettings.activeBuildTarget)
                {
                    case BuildTarget.Android: return Platform.Android;
                    case BuildTarget.iOS: return Platform.iOS;
                    case BuildTarget.StandaloneWindows64: return Platform.PC;
                    case BuildTarget.StandaloneLinux64: return Platform.PC; // for CI
                    default: return null;
                }
            }
        }
        
        protected override void Execute(ndmf.BuildContext context)
        {
            ExecuteStatic(context);
        }

        internal static void ExecuteStatic(ndmf.BuildContext context)
        {
            var refObj = LastPrimaryTarget.Value ?? context.AvatarRootObject;

            var components = refObj.GetComponentsInChildren<ModularAvatarSyncParameterSequence>(true);
            if (components.Length == 0) return;
            if (components.Length > 1)
            {
                BuildReport.LogFatal("error.singleton", "Sync Parameter Sequence", components.Cast<object>().ToArray());
                return;
            }

            var primaryPlatform = components[0].PrimaryPlatform;

            var target = OverrideBuildTarget ?? EditorUserBuildSettings.activeBuildTarget;

            var targetDesc = context.VRChatAvatarDescriptor();
            ParameterInfoRegistry.Instance.NormalizeParameters(context, targetDesc,
                target, CurrentPlatform == primaryPlatform);
        }
    }
}

#endif