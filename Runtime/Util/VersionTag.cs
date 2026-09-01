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
