#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    internal class ParameterInfoRegistry
    {
        public static ParameterInfoRegistry Instance { get; } = new(ParameterInfoStore.Instance);

        public ParameterInfoRegistry(IParameterInfoStore store)
        {
            _store = store;
        }

        private readonly IParameterInfoStore _store;

        public event Action? OnInconsistentBlueprintDetected;
        public ImmutableHashSet<string> InconsistentBlueprints { get; private set; } = ImmutableHashSet<string>.Empty;

        private static List<ParameterDefinition> ConvertParameters(VRCExpressionParameters.Parameter[]? parameters)
        {
            var parameterInfo = parameters ?? Array.Empty<VRCExpressionParameters.Parameter>();

            var convertedInfo = parameterInfo
                .Where(p => p.networkSynced)
                .Select(ParameterDefinition.FromVRC)
                .ToList();
            return convertedInfo;
        }

        private static string? GetBlueprintId(VRCAvatarDescriptor descriptor)
        {
            var pipelineManager = descriptor.GetComponent<PipelineManager>();
            var blueprintId = pipelineManager?.blueprintId;
            return blueprintId;
        }

        public void NormalizeParameters(ndmf.BuildContext context, VRCAvatarDescriptor desc,
            BuildTarget activeBuildTarget, bool isPrimaryPlatform)
        {
            var blueprintId = GetBlueprintId(desc);
            if (blueprintId == null) return;


            VRCExpressionParameters newExpressionParametersAsset;
            if (desc.expressionParameters == null)
            {
                // TODO: load default parameters
                newExpressionParametersAsset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            }
            else if (context.IsTemporaryAsset(desc.expressionParameters))
            {
                newExpressionParametersAsset = desc.expressionParameters;
            }
            else
            {
                newExpressionParametersAsset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                newExpressionParametersAsset.parameters = (desc.expressionParameters.parameters
                    ?? Array.Empty<VRCExpressionParameters.Parameter>()).ToArray();
            }

            desc.expressionParameters = newExpressionParametersAsset;

            // First, sort parameters to get a consistent order
            Array.Sort(newExpressionParametersAsset.parameters,
                (p1, p2) => string.Compare(p1.name, p2.name, StringComparison.Ordinal));

            var wantedParameters = ConvertParameters(newExpressionParametersAsset.parameters);

            // Merge in anything from the primary platform
            if (!isPrimaryPlatform)
            {
                var current = _store.GetRecordForBlueprintId(blueprintId);
                if (current.PrimaryPlatformRecord != null)
                {
                    List<VRCExpressionParameters.Parameter> parameters =
                        newExpressionParametersAsset.parameters.ToList();

                    var knownParameters = newExpressionParametersAsset.parameters.ToDictionary(p => p.name, p => p);
                    foreach (var parameter in current.PrimaryPlatformRecord.WantedParameters)
                    {
                        var toVRC = parameter.ToVRC();
                        if (knownParameters.TryGetValue(parameter.Name, out var existing))
                        {
                            if (existing.valueType != toVRC.valueType)
                            {
                                BuildReport.LogFatal("error.syncparamsequence.type_mismatch",
                                    parameter.Name,
                                    existing.valueType, EditorUserBuildSettings.activeBuildTarget,
                                    toVRC.valueType, current.PrimaryPlatformRecord.Target
                                );
                            }
                            knownParameters.Remove(parameter.Name);
                        }
                        else
                        {
                            parameters.Add(toVRC);
                        }
                    }

                    foreach (var parameter in knownParameters.Keys)
                    {
                        if (!knownParameters[parameter].networkSynced) continue;

                        BuildReport.LogFatal("error.syncparamsequence.unregistered_parameter", parameter,
                            current.PrimaryPlatformRecord.Target);
                    }

                    var totalCost = parameters.Where(p => p.networkSynced)
                        .Sum(p => VRCExpressionParameters.TypeCost(p.valueType));
                    if (totalCost >
                        VRCExpressionParameters.MAX_PARAMETER_COST)
                    {
                        BuildReport.LogFatal("error.syncparamsequence.cost_exceeded",
                            current.PrimaryPlatformRecord.Target, totalCost,
                            VRCExpressionParameters.MAX_PARAMETER_COST);
                    }
                    
                    newExpressionParametersAsset.parameters = parameters.ToArray();

                    Array.Sort(newExpressionParametersAsset.parameters,
                        (p1, p2) => string.Compare(p1.name, p2.name, StringComparison.Ordinal));
                }
            }

            var actualParameters = ConvertParameters(newExpressionParametersAsset.parameters);

            var updated = _store.UpdateRecordForPlatform(blueprintId, isPrimaryPlatform, new ParameterInfoRecord
            {
                Target = activeBuildTarget,
                WantedParameters = wantedParameters,
                ActualParameters = actualParameters
            });

            if (updated.IsConsistent)
            {
                InconsistentBlueprints = InconsistentBlueprints.Remove(blueprintId);
            }
            else
            {
                InconsistentBlueprints = InconsistentBlueprints.Add(blueprintId);
                OnInconsistentBlueprintDetected?.Invoke();
            }
        }
    }
}

#endif