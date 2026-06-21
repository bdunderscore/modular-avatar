#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.ModularAvatarSyncParameterSequence;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    internal static class SyncParameterSequenceDebugLog
    {
        private const string EditorPrefKey = "nadena.dev.modular-avatar.sync-parameter-sequence.debug-log";
        private const string LogDirectory = "Logs/nadena.dev.modular-avatar/SyncParameterSequence";
        private const string LogFileName = "sync-parameter-sequence.log";

        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(EditorPrefKey, false);
            private set => EditorPrefs.SetBool(EditorPrefKey, value);
        }

        internal static Context? CreateContext(
            VRCAvatarDescriptor avatarDescriptor,
            BuildTarget currentBuildPlatform,
            GameObject avatarRootObject)
        {
            return Enabled
                ? new Context(DateTimeOffset.Now, avatarDescriptor.name, currentBuildPlatform, avatarRootObject.name)
                : null;
        }

        [MenuItem(UnityMenuItems.TopMenu_SyncParameterSequenceDebugLog, false,
            UnityMenuItems.TopMenu_SyncParameterSequenceDebugLogOrder)]
        private static void ToggleDebugLog()
        {
            Enabled = !Enabled;
        }

        [MenuItem(UnityMenuItems.TopMenu_SyncParameterSequenceDebugLog, true,
            UnityMenuItems.TopMenu_SyncParameterSequenceDebugLogOrder)]
        private static bool ToggleDebugLogValidate()
        {
            Menu.SetChecked(UnityMenuItems.TopMenu_SyncParameterSequenceDebugLog, Enabled);
            return true;
        }

        internal sealed class Context
        {
            private readonly DateTimeOffset _timestamp;
            private readonly string _avatarName;
            private readonly BuildTarget _currentBuildPlatform;
            private readonly string _avatarRootObjectName;

            private Platform? _primaryPlatform;
            private AvatarRecord? _initialRegistry;

            private VRCExpressionParameters.Parameter[] _initialParameters =
                Array.Empty<VRCExpressionParameters.Parameter>();

            private VRCExpressionParameters.Parameter[] _finalParameters =
                Array.Empty<VRCExpressionParameters.Parameter>();

            private List<ParameterDefinition> _syncedAvatarParameters = new();
            private AvatarRecord? _finalRegistry;
            private readonly List<string> _anomalies = new();

            internal Context(
                DateTimeOffset timestamp,
                string avatarName,
                BuildTarget currentBuildPlatform,
                string avatarRootObjectName)
            {
                _timestamp = timestamp;
                _avatarName = avatarName;
                _currentBuildPlatform = currentBuildPlatform;
                _avatarRootObjectName = avatarRootObjectName;
            }

            internal void SetPrimaryPlatform(Platform primaryPlatform)
            {
                _primaryPlatform = primaryPlatform;
            }

            internal void SetInitialRegistry(AvatarRecord record)
            {
                _initialRegistry = CloneAvatarRecord(record);
            }

            internal void SetInitialParameters(VRCExpressionParameters.Parameter[]? parameters)
            {
                _initialParameters = CloneParameters(parameters);
            }

            internal void SetFinalParameters(VRCExpressionParameters.Parameter[]? parameters)
            {
                _finalParameters = CloneParameters(parameters);
            }

            internal void SetSyncedAvatarParameters(IReadOnlyList<ParameterDefinition> parameters)
            {
                _syncedAvatarParameters = CloneParameterDefinitions(parameters);
            }

            internal void SetFinalRegistry(AvatarRecord record)
            {
                _finalRegistry = CloneAvatarRecord(record);
            }

            internal void AddAnomaly(string message)
            {
                _anomalies.Add(message);
            }

            internal void Append()
            {
                Write(Render());
            }

            internal void Append(string message)
            {
                var sb = new StringBuilder();
                AppendHeader(sb);
                sb.AppendLine(message);
                AppendFooter(sb);
                Write(sb.ToString());
            }

            private string Render()
            {
                var sb = new StringBuilder();
                AppendHeader(sb);

                sb.AppendLine("Initial platform registry:");
                AppendAvatarRecord(sb, _initialRegistry, "  ");

                sb.AppendLine();
                sb.AppendLine("Initial expression parameters:");
                AppendAvatarParameters(sb, _initialParameters, "  ");

                sb.AppendLine();
                sb.AppendLine("Final expression parameters:");
                AppendAvatarParameters(sb, _finalParameters, "  ");

                sb.AppendLine();
                sb.AppendLine("Expression parameter diff from initial to final:");
                AppendExpressionParameterDiff(sb, _initialParameters, _finalParameters);

                sb.AppendLine();
                sb.AppendLine("Synced parameter diff against registry primary platform:");
                AppendDiff(sb, _syncedAvatarParameters, _initialRegistry?.PrimaryPlatformRecord?.WantedParameters);

                sb.AppendLine();
                sb.AppendLine("Anomalies:");
                AppendAnomalies(sb, "  ");

                sb.AppendLine();
                sb.AppendLine("Final platform registry:");
                AppendAvatarRecord(sb, _finalRegistry, "  ");

                AppendFooter(sb);
                return sb.ToString();
            }

            private void AppendHeader(StringBuilder sb)
            {
                sb.AppendLine(new string('=', 80));
                sb.AppendLine("Sync Parameter Sequence debug log");
                sb.AppendLine("Timestamp: " + _timestamp.ToString("O"));
                sb.AppendLine("Avatar name: " + _avatarName);
                sb.AppendLine("Current build platform: " + _currentBuildPlatform);
                sb.AppendLine("Avatar root object name: " + _avatarRootObjectName);
                sb.AppendLine("Primary platform selected by component: " +
                              (_primaryPlatform?.ToString() ?? "(none)"));
                sb.AppendLine();
            }

            private static void AppendFooter(StringBuilder sb)
            {
                sb.AppendLine(new string('=', 80));
                sb.AppendLine();
            }

            private static void Write(string contents)
            {
                try
                {
                    var directory = Path.Combine(Directory.GetCurrentDirectory(), LogDirectory);
                    Directory.CreateDirectory(directory);

                    var path = Path.Combine(directory, LogFileName);
                    File.AppendAllText(path, contents, Encoding.UTF8);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            private static AvatarRecord CloneAvatarRecord(AvatarRecord record)
            {
                var clone = new AvatarRecord
                {
                    PrimaryPlatformRecord = CloneParameterInfoRecord(record.PrimaryPlatformRecord)
                };

                foreach (var secondaryRecord in record.SecondaryPlatformRecords)
                {
                    var clonedRecord = CloneParameterInfoRecord(secondaryRecord);
                    if (clonedRecord != null) clone.SecondaryPlatformRecords.Add(clonedRecord);
                }

                return clone;
            }

            private static ParameterInfoRecord? CloneParameterInfoRecord(ParameterInfoRecord? record)
            {
                if (record == null) return null;

                return new ParameterInfoRecord
                {
                    LastUpdated = record.LastUpdated,
                    Target = record.Target,
                    WantedParameters = CloneParameterDefinitions(record.WantedParameters),
                    ActualParameters = CloneParameterDefinitions(record.ActualParameters)
                };
            }

            private static List<ParameterDefinition> CloneParameterDefinitions(
                IReadOnlyList<ParameterDefinition> parameters)
            {
                return parameters.Select(parameter => new ParameterDefinition
                {
                    Name = parameter.Name,
                    ValueType = parameter.ValueType,
                    DefaultValue = parameter.DefaultValue,
                    Saved = parameter.Saved
                }).ToList();
            }

            private static VRCExpressionParameters.Parameter[] CloneParameters(
                VRCExpressionParameters.Parameter[]? parameters)
            {
                return (parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
                    .Select(parameter => new VRCExpressionParameters.Parameter
                    {
                        name = parameter.name,
                        valueType = parameter.valueType,
                        defaultValue = parameter.defaultValue,
                        saved = parameter.saved,
                        networkSynced = parameter.networkSynced
                    })
                    .ToArray();
            }

            private static void AppendAvatarRecord(StringBuilder sb, AvatarRecord? record, string indent)
            {
                if (record == null)
                {
                    sb.AppendLine(indent + "(not captured)");
                    return;
                }

                if (record.PrimaryPlatformRecord == null && record.SecondaryPlatformRecords.Count == 0)
                {
                    sb.AppendLine(indent + "(empty)");
                    return;
                }

                sb.AppendLine(indent + "Primary platform:");
                AppendParameterInfoRecord(sb, record.PrimaryPlatformRecord, indent + "  ");

                sb.AppendLine(indent + "Secondary platforms:");
                if (record.SecondaryPlatformRecords.Count == 0)
                {
                    sb.AppendLine(indent + "  (none)");
                    return;
                }

                foreach (var platformRecord in record.SecondaryPlatformRecords.OrderBy(r => r.Target.ToString()))
                {
                    AppendParameterInfoRecord(sb, platformRecord, indent + "  ");
                }
            }

            private static void AppendParameterInfoRecord(StringBuilder sb, ParameterInfoRecord? record, string indent)
            {
                if (record == null)
                {
                    sb.AppendLine(indent + "(none)");
                    return;
                }

                sb.AppendLine(indent + "Target: " + record.Target);
                sb.AppendLine(indent + "Last updated: " + record.LastUpdated.ToString("O"));
                sb.AppendLine(indent + "Wanted parameters:");
                AppendParameterDefinitions(sb, record.WantedParameters, indent + "  ");
                sb.AppendLine(indent + "Actual parameters:");
                AppendParameterDefinitions(sb, record.ActualParameters, indent + "  ");
            }

            private static void AppendAvatarParameters(
                StringBuilder sb,
                IReadOnlyList<VRCExpressionParameters.Parameter> parameters,
                string indent)
            {
                if (parameters.Count == 0)
                {
                    sb.AppendLine(indent + "(none)");
                    return;
                }

                foreach (var parameter in parameters.OrderBy(p => p.name, StringComparer.Ordinal))
                {
                    sb.AppendLine(indent + FormatVrcParameter(parameter));
                }
            }

            private static void AppendParameterDefinitions(
                StringBuilder sb,
                IReadOnlyList<ParameterDefinition> parameters,
                string indent)
            {
                if (parameters.Count == 0)
                {
                    sb.AppendLine(indent + "(none)");
                    return;
                }

                foreach (var parameter in parameters.OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    sb.AppendLine(indent + FormatParameterDefinition(parameter));
                }
            }

            private void AppendAnomalies(StringBuilder sb, string indent)
            {
                if (_anomalies.Count == 0)
                {
                    sb.AppendLine(indent + "(none)");
                    return;
                }

                foreach (var anomaly in _anomalies)
                {
                    sb.AppendLine(indent + anomaly);
                }
            }

            private void AppendExpressionParameterDiff(
                StringBuilder sb,
                IReadOnlyList<VRCExpressionParameters.Parameter> initialParameters,
                IReadOnlyList<VRCExpressionParameters.Parameter> finalParameters)
            {
                var initialByName = ParametersByName(initialParameters, "initial expression parameters");
                var finalByName = ParametersByName(finalParameters, "final expression parameters");

                var onlyInitial = initialByName.Keys
                    .Except(finalByName.Keys, StringComparer.Ordinal)
                    .Select(name => initialByName[name])
                    .ToList();
                var onlyFinal = finalByName.Keys
                    .Except(initialByName.Keys, StringComparer.Ordinal)
                    .Select(name => finalByName[name])
                    .ToList();
                var changed = initialByName.Keys
                    .Intersect(finalByName.Keys, StringComparer.Ordinal)
                    .Where(name => !ParametersEqual(initialByName[name], finalByName[name]))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Select(name => (Name: name, Initial: initialByName[name], Final: finalByName[name]))
                    .ToList();

                sb.AppendLine("  Only in initial expression parameters:");
                AppendAvatarParameters(sb, onlyInitial, "    ");
                sb.AppendLine("  Only in final expression parameters:");
                AppendAvatarParameters(sb, onlyFinal, "    ");
                sb.AppendLine("  Changed expression parameters:");
                if (changed.Count == 0)
                {
                    sb.AppendLine("    (none)");
                    return;
                }

                foreach (var change in changed)
                {
                    sb.AppendLine("    " + change.Name);
                    sb.AppendLine("      initial: " + FormatVrcParameter(change.Initial));
                    sb.AppendLine("      final:   " + FormatVrcParameter(change.Final));
                }
            }

            private void AppendDiff(
                StringBuilder sb,
                IReadOnlyList<ParameterDefinition> avatarParameters,
                IReadOnlyList<ParameterDefinition>? primaryPlatformParameters)
            {
                if (primaryPlatformParameters == null)
                {
                    sb.AppendLine("  Registry primary platform parameters: (none)");
                    sb.AppendLine("  Only on avatar:");
                    AppendParameterDefinitions(sb, avatarParameters, "    ");
                    sb.AppendLine("  Only in registry primary platform:");
                    sb.AppendLine("    (none)");
                    return;
                }

                var avatarByName = ParameterDefinitionsByName(avatarParameters, "synced avatar parameters");
                var primaryByName = ParameterDefinitionsByName(primaryPlatformParameters,
                    "registry primary platform wanted parameters");

                var onlyAvatar = avatarByName.Keys
                    .Except(primaryByName.Keys, StringComparer.Ordinal)
                    .Select(name => avatarByName[name])
                    .ToList();
                var onlyPrimary = primaryByName.Keys
                    .Except(avatarByName.Keys, StringComparer.Ordinal)
                    .Select(name => primaryByName[name])
                    .ToList();

                sb.AppendLine("  Only on avatar:");
                AppendParameterDefinitions(sb, onlyAvatar, "    ");
                sb.AppendLine("  Only in registry primary platform:");
                AppendParameterDefinitions(sb, onlyPrimary, "    ");
            }

            private static string FormatVrcParameter(VRCExpressionParameters.Parameter parameter)
            {
                return $"{parameter.name} type={parameter.valueType} default={parameter.defaultValue} " +
                       $"saved={parameter.saved} networkSynced={parameter.networkSynced}";
            }

            private Dictionary<string, VRCExpressionParameters.Parameter> ParametersByName(
                IReadOnlyList<VRCExpressionParameters.Parameter> parameters,
                string source)
            {
                var groups = parameters
                    .GroupBy(parameter => parameter.name ?? string.Empty, StringComparer.Ordinal)
                    .ToList();

                foreach (var group in groups.Where(group => group.Count() > 1).OrderBy(group => group.Key))
                {
                    AddAnomaly(
                        $"Duplicate parameter name '{group.Key}' found in {source}; using the last occurrence.");
                }

                var lookup = new Dictionary<string, VRCExpressionParameters.Parameter>(StringComparer.Ordinal);
                foreach (var group in groups)
                {
                    lookup[group.Key] = group.Last();
                }

                return lookup;
            }

            private Dictionary<string, ParameterDefinition> ParameterDefinitionsByName(
                IReadOnlyList<ParameterDefinition> parameters,
                string source)
            {
                var groups = parameters
                    .GroupBy(parameter => parameter.Name ?? string.Empty, StringComparer.Ordinal)
                    .ToList();

                foreach (var group in groups.Where(group => group.Count() > 1).OrderBy(group => group.Key))
                {
                    AddAnomaly(
                        $"Duplicate parameter name '{group.Key}' found in {source}; using the last occurrence.");
                }

                var lookup = new Dictionary<string, ParameterDefinition>(StringComparer.Ordinal);
                foreach (var group in groups)
                {
                    lookup[group.Key] = group.Last();
                }

                return lookup;
            }

            private static bool ParametersEqual(
                VRCExpressionParameters.Parameter initialParameter,
                VRCExpressionParameters.Parameter finalParameter)
            {
                return initialParameter.valueType == finalParameter.valueType &&
                       initialParameter.defaultValue.Equals(finalParameter.defaultValue) &&
                       initialParameter.saved == finalParameter.saved &&
                       initialParameter.networkSynced == finalParameter.networkSynced;
            }

            private static string FormatParameterDefinition(ParameterDefinition parameter)
            {
                return $"{parameter.Name} type={parameter.ValueType} default={parameter.DefaultValue} " +
                       $"saved={parameter.Saved}";
            }
        }
    }
}

#endif
