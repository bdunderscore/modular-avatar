#nullable enable

using System;
using System.Collections.Generic;

namespace nadena.dev.modular_avatar
{
    internal sealed class SemVerComparator : IComparer<string>
    {
        public static SemVerComparator Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            SemanticVersion xVersion;
            SemanticVersion yVersion;
            try
            {
                xVersion = SemanticVersion.Parse(x, nameof(x));
            }
            catch (ArgumentException)
            {
                xVersion = SemanticVersion.Zero;
            }

            try
            {
                yVersion = SemanticVersion.Parse(y, nameof(y));
            }
            catch (ArgumentException)
            {
                yVersion = SemanticVersion.Zero;
            }

            var comparison = CompareNumericIdentifier(xVersion.Major, yVersion.Major);
            if (comparison != 0) return comparison;

            comparison = CompareNumericIdentifier(xVersion.Minor, yVersion.Minor);
            if (comparison != 0) return comparison;

            comparison = CompareNumericIdentifier(xVersion.Patch, yVersion.Patch);
            if (comparison != 0) return comparison;

            return ComparePrerelease(xVersion.Prerelease, yVersion.Prerelease);
        }

        /// <summary>
        ///     Compares versions for compatibility checks, treating all prereleases of a version as equivalent to that
        ///     version's stable release.
        /// </summary>
        public int CompareForCompatibility(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            SemanticVersion xVersion;
            SemanticVersion yVersion;
            try
            {
                xVersion = SemanticVersion.Parse(x, nameof(x));
            }
            catch (ArgumentException)
            {
                xVersion = SemanticVersion.Zero;
            }

            try
            {
                yVersion = SemanticVersion.Parse(y, nameof(y));
            }
            catch (ArgumentException)
            {
                yVersion = SemanticVersion.Zero;
            }

            var comparison = CompareNumericIdentifier(xVersion.Major, yVersion.Major);
            if (comparison != 0) return comparison;

            comparison = CompareNumericIdentifier(xVersion.Minor, yVersion.Minor);
            if (comparison != 0) return comparison;

            return CompareNumericIdentifier(xVersion.Patch, yVersion.Patch);
        }

        private static int ComparePrerelease(string[]? x, string[]? y)
        {
            if (x == null) return y == null ? 0 : 1;
            if (y == null) return -1;

            var identifierCount = Math.Min(x.Length, y.Length);
            for (var i = 0; i < identifierCount; i++)
            {
                var xIsNumeric = IsNumeric(x[i]);
                var yIsNumeric = IsNumeric(y[i]);

                int comparison;
                if (xIsNumeric && yIsNumeric)
                {
                    comparison = CompareNumericIdentifier(x[i], y[i]);
                }
                else if (xIsNumeric)
                {
                    comparison = -1;
                }
                else if (yIsNumeric)
                {
                    comparison = 1;
                }
                else
                {
                    comparison = string.CompareOrdinal(x[i], y[i]);
                }

                if (comparison != 0) return comparison;
            }

            return x.Length.CompareTo(y.Length);
        }

        private static int CompareNumericIdentifier(string x, string y)
        {
            if (x.Length != y.Length) return x.Length.CompareTo(y.Length);
            return string.CompareOrdinal(x, y);
        }

        private static bool IsNumeric(string identifier)
        {
            foreach (var character in identifier)
            {
                if (character < '0' || character > '9') return false;
            }

            return true;
        }

        private readonly struct SemanticVersion
        {
            public static readonly SemanticVersion Zero = new("0", "0", "0", null);

            public string Major { get; }
            public string Minor { get; }
            public string Patch { get; }
            public string[]? Prerelease { get; }

            private SemanticVersion(string major, string minor, string patch, string[]? prerelease)
            {
                Major = major;
                Minor = minor;
                Patch = patch;
                Prerelease = prerelease;
            }

            public static SemanticVersion Parse(string value, string parameterName)
            {
                var buildMetadataSeparator = value.IndexOf('+');
                var versionAndPrerelease = buildMetadataSeparator == -1
                    ? value
                    : value.Substring(0, buildMetadataSeparator);

                if (buildMetadataSeparator != -1)
                {
                    ValidateIdentifiers(value.Substring(buildMetadataSeparator + 1), false, value, parameterName);
                }

                var prereleaseSeparator = versionAndPrerelease.IndexOf('-');
                var version = prereleaseSeparator == -1
                    ? versionAndPrerelease
                    : versionAndPrerelease.Substring(0, prereleaseSeparator);

                var numericIdentifiers = version.Split('.');
                if (numericIdentifiers.Length != 3)
                {
                    ThrowInvalidVersion(value, parameterName);
                }

                foreach (var identifier in numericIdentifiers)
                {
                    ValidateNumericIdentifier(identifier, value, parameterName);
                }

                string[]? prerelease = null;
                if (prereleaseSeparator != -1)
                {
                    prerelease = ValidateIdentifiers(
                        versionAndPrerelease.Substring(prereleaseSeparator + 1),
                        true,
                        value,
                        parameterName
                    );
                }

                return new SemanticVersion(numericIdentifiers[0], numericIdentifiers[1], numericIdentifiers[2],
                    prerelease);
            }

            private static string[] ValidateIdentifiers(
                string value,
                bool rejectLeadingZeros,
                string fullVersion,
                string parameterName
            )
            {
                var identifiers = value.Split('.');
                foreach (var identifier in identifiers)
                {
                    if (identifier.Length == 0)
                    {
                        ThrowInvalidVersion(fullVersion, parameterName);
                    }

                    foreach (var character in identifier)
                    {
                        var isDigit = character >= '0' && character <= '9';
                        var isLetter = (character >= 'A' && character <= 'Z')
                                       || (character >= 'a' && character <= 'z');
                        if (!isDigit && !isLetter && character != '-')
                        {
                            ThrowInvalidVersion(fullVersion, parameterName);
                        }
                    }

                    if (rejectLeadingZeros && identifier.Length > 1 && identifier[0] == '0' && IsNumeric(identifier))
                    {
                        ThrowInvalidVersion(fullVersion, parameterName);
                    }
                }

                return identifiers;
            }

            private static void ValidateNumericIdentifier(string value, string fullVersion, string parameterName)
            {
                if (value.Length == 0 || (value.Length > 1 && value[0] == '0') || !IsNumeric(value))
                {
                    ThrowInvalidVersion(fullVersion, parameterName);
                }
            }

            private static void ThrowInvalidVersion(string value, string parameterName)
            {
                throw new ArgumentException($"'{value}' is not a valid semantic version.", parameterName);
            }
        }
    }
}