#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Linq;
using System.Threading;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.vrchat;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
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
            var target = OverrideBuildTarget ?? EditorUserBuildSettings.activeBuildTarget;
            var targetDesc = context.VRChatAvatarDescriptor();
            var debugContext = SyncParameterSequenceDebugLog.CreateContext(
                targetDesc,
                target,
                context.AvatarRootObject);

            var components = refObj.GetComponentsInChildren<ModularAvatarSyncParameterSequence>(true);
            if (components.Length == 0)
            {
                debugContext?.Append("Skipped: No Sync Parameter Sequence component is present.");
                return;
            }

            if (components.Length > 1)
            {
                BuildReport.LogFatal("error.singleton", "Sync Parameter Sequence", components.Cast<object>().ToArray());
                return;
            }

            var component = components[0];
            var primaryPlatform = component.PrimaryPlatform;
            var currentPlatform = CurrentPlatform;

            if (currentPlatform == null)
            {
                debugContext?.Append(
                    $"Skipped: Current platform ({target}) is not supported for Sync Parameter Sequence");
                return;
            }
            
            var isPrimaryPlatform = currentPlatform == primaryPlatform;
            debugContext?.SetPrimaryPlatform(primaryPlatform);
            
            WarnIfPrimaryPlatformChanged(component, targetDesc, primaryPlatform);
            WarnIfNoPrimaryRecordAndSecondary(component, targetDesc, target, isPrimaryPlatform);

            ParameterInfoRegistry.Instance.NormalizeParameters(context, targetDesc,
                target, isPrimaryPlatform, debugContext);
        }

        private static void WarnIfPrimaryPlatformChanged(
            ModularAvatarSyncParameterSequence component,
            VRCAvatarDescriptor targetDesc,
            Platform primaryPlatform)
        {
            var blueprintId = ParameterInfoRegistry.GetBlueprintId(targetDesc);
            if (blueprintId == null) return;

            var record = ParameterInfoStore.Instance.GetRecordForBlueprintId(blueprintId);
            if (record.PrimaryPlatformRecord == null) return;

            var previousPlatform = PlatformFromBuildTarget(record.PrimaryPlatformRecord.Target);
            if (previousPlatform != primaryPlatform)
            {
                BuildReport.ReportingObject(component, () =>
                    BuildReport.Log(ErrorSeverity.NonFatal,
                        "validation.syncparamsequence.primary_platform_changed",
                        primaryPlatform,
                        record.PrimaryPlatformRecord.Target));
            }
        }

        private static void WarnIfNoPrimaryRecordAndSecondary(
            ModularAvatarSyncParameterSequence component,
            VRCAvatarDescriptor targetDesc,
            BuildTarget target,
            bool isPrimaryPlatform)
        {
            if (isPrimaryPlatform) return;

            var blueprintId = ParameterInfoRegistry.GetBlueprintId(targetDesc);
            if (blueprintId == null) return;

            var record = ParameterInfoStore.Instance.GetRecordForBlueprintId(blueprintId);
            if (record.PrimaryPlatformRecord != null) return;

            BuildReport.ReportingObject(component, () =>
                BuildReport.Log(ErrorSeverity.NonFatal,
                    "validation.syncparamsequence.no_primary_record",
                    target,
                    component.PrimaryPlatform));
        }

        private static Platform? PlatformFromBuildTarget(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.Android: return Platform.Android;
                case BuildTarget.iOS: return Platform.iOS;
                case BuildTarget.StandaloneWindows64: return Platform.PC;
                case BuildTarget.StandaloneLinux64: return Platform.PC; // for CI
                default: return null;
            }
        }
    }
}

#endif
