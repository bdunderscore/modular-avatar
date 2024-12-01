#nullable enable

using System;
using System.Collections.Specialized;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.ModularAvatarSyncParameterSequence;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    public class SyncParameterSequencePass : Pass<SyncParameterSequencePass>
    {
        private static Platform? CurrentPlatform
        {
            get
            {
                switch (EditorUserBuildSettings.activeBuildTarget)
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
            var avDesc = context.AvatarDescriptor;
            
            var components = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarSyncParameterSequence>(true);
            if (components.Length == 0) return;
            if (components.Length > 1)
            {
                BuildReport.LogFatal("error.singleton", "Sync Parameter Sequence", components.Cast<object>().ToArray());
                return;
            }
            
            var syncComponent = components[0];
            if (syncComponent.Parameters == null) return;

            if (avDesc.expressionParameters == null) return;
            var avatarParams = avDesc.expressionParameters;

            if (!context.IsTemporaryAsset(avatarParams))
            {
                avatarParams = Object.Instantiate(avatarParams);
                avDesc.expressionParameters = avatarParams;
            }

            if (syncComponent.Parameters.parameters == null)
            {
                syncComponent.Parameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                EditorUtility.SetDirty(syncComponent.Parameters);
            }
            
            // If we're on the primary platform, add in any unknown parameters, and prune if we exceed the limit.
            if (CurrentPlatform != null && CurrentPlatform == syncComponent.PrimaryPlatform)
            {
                var registered = new OrderedDictionary();

                foreach (var param in syncComponent.Parameters.parameters)
                {
                    if (param == null) continue;
                    if (!param.networkSynced) continue;
                    registered[param.name] = param;
                }

                foreach (var param in avatarParams.parameters)
                {
                    if (param == null) continue;
                    if (!param.networkSynced) continue;
                    registered[param.name] = param;
                }

                syncComponent.Parameters.parameters = registered.Values.Cast<VRCExpressionParameters.Parameter>().ToArray();
                if (!syncComponent.Parameters.IsWithinBudget())
                {
                    var knownParams = avatarParams.parameters.Where(p => p != null).Select(p => p.name).ToHashSet();
                    syncComponent.Parameters.parameters = syncComponent.Parameters.parameters.Where(
                        p => p != null && knownParams.Contains(p.name)
                    ).ToArray();
                }
                
                EditorUtility.SetDirty(syncComponent.Parameters);
            }
            
            // Now copy back...
            OrderedDictionary finalParams = new();
            foreach (var param in syncComponent.Parameters.parameters)
            {
                if (param == null) continue;
                if (!param.networkSynced) continue;
                finalParams[param.name] = param;
            }
            
            foreach (var param in avatarParams.parameters)
            {
                if (param == null) continue;
                finalParams[param.name] = param;
            }
            
            avatarParams.parameters = finalParams.Values.Cast<VRCExpressionParameters.Parameter>().ToArray();
            
            EditorUtility.SetDirty(avatarParams);
        }
    }
}