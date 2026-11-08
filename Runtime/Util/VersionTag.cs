#nullable enable

using System;
using System.Collections.Generic;

namespace nadena.dev.modular_avatar
{
    [Serializable]
    internal struct VersionTag
    {
        private static readonly Dictionary<string, string> CompatibilityVersionOverrideMappings;

        static VersionTag()
        {
            CompatibilityVersionOverrideMappings = new Dictionary<string, string>();
            // No serialization changes in this version
            CompatibilityVersionOverrideMappings["1.19.0-alpha.0"] = "1.18.0";
        }
        
        public string? UpdatedAtVersion;
        public string? MinimumVersion;

        public static VersionTag Current { get; private set; }

        /// <summary>
        ///     Updates the installed Modular Avatar version. This is called by the editor-only package metadata lookup.
        /// </summary>
        public static void SetCurrentVersion(string version)
        {
            var versionParts = version.Split('.');

            if (versionParts.Length >= 3 && version.Contains("-") && versionParts[2].StartsWith("0-") &&
                versionParts[1] != "0")
            {
                // First beta in a minor release series. We deliberately store the _prior_ minor version here,
                // as it's not uncommon for asset creators to use betas, and we don't want to confuse users
                // with upgrade reminders to future beta versions. We'll display a warning on the export dialog
                // separately.
                versionParts[1] = "" + (int.Parse(versionParts[1]) - 1);
            }
            
            var minimumVersion = versionParts.Length >= 2
                ? $"{versionParts[0]}.{versionParts[1]}.0"
                : version;

            if (CompatibilityVersionOverrideMappings.TryGetValue(version, out var overrideVersion))
            {
                minimumVersion = overrideVersion;
            }

            Current = new VersionTag
            {
                UpdatedAtVersion = version,
                MinimumVersion = minimumVersion
            };
            CompatCache.Clear();
        }

        public bool IsPrerelease
        {
            get
            {
                if (UpdatedAtVersion == null) return false;

                var versionParts = UpdatedAtVersion.Split('.');

                return versionParts?.Length >= 3 && UpdatedAtVersion.Contains("-") &&
                       versionParts[2].StartsWith("0-") && versionParts[1] != "0";
            }
        }
        
        public bool IsCompatible
        {
            get
            {
                var minimumVersion = MinimumVersion;
                if (minimumVersion == null || string.IsNullOrWhiteSpace(minimumVersion))
                {
                    return true;
                }

                var currentVersion = Current.UpdatedAtVersion;
                if (currentVersion == null) return false;

                if (CompatCache.TryGetValue(minimumVersion, out var cachedResult))
                {
                    return cachedResult;
                }

                bool isCompatible;
                try
                {
                    isCompatible = SemVerComparator.CompareForCompatibility(currentVersion, minimumVersion) >= 0;
                }
                catch (Exception)
                {
                    isCompatible = true;
                }

                CompatCache[minimumVersion] = isCompatible;
                return isCompatible;
            }
        }

        private static Dictionary<string, bool> CompatCache = new();
        private static readonly SemVerComparator SemVerComparator = new();
    }
}
