#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace nadena.dev.modular_avatar.editor.version
{
    internal sealed class VersionMismatchWindow : EditorWindow
    {
        private const string RootPath = "Packages/nadena.dev.modular-avatar/Editor/VersionCheck/";
        private const string UxmlPath = RootPath + "VersionMismatchWindow.uxml";
        private const string UssPath = RootPath + "VersionMismatchWindow.uss";
        private const string PendingRefreshKey = "nadena.dev.modular-avatar.version-mismatch.pending-refresh";
        private const string UpgradeGuideUrl = "https://modular-avatar.nadena.dev/docs/upgrading?lang=auto";

        private const string DevelopmentUpgradeGuideUrl =
            "https://modular-avatar.nadena.dev/dev/docs/upgrading?lang=auto";

        [SerializeField] private string requiredVersion = "1.18.0";
        [SerializeField] private string installedVersion = "1.17.0";
        [SerializeField] private List<string> affectedAssets = new();
        [SerializeField] private bool isConfigured;
        [SerializeField] private string? packageManifestContents;

        [NonSerialized] private bool configuredThisDomain;

        private static readonly string[] TestAssets =
        {
            "Assets/Example Avatar/Avatar.prefab",
            "Assets/Example Avatar/Outfit.prefab",
            "Assets/Example Avatar/Accessories/Hat.prefab",
            "Assets/Example Avatar/Accessories/Glasses.prefab",
            "Assets/Example Avatar/Expressions/Menu.asset"
        };

        [Serializable]
        private sealed class PendingRefresh
        {
            public string requiredVersion = "";
            public List<string> affectedAssets = new();
        }

        [Serializable]
        private sealed class PackageManifest
        {
            public string version = "";
        }

        [InitializeOnLoadMethod]
        private static void ReopenAfterDomainReload()
        {
            EditorApplication.delayCall += ReopenAfterPackageRefresh;
        }

        internal static void ShowWindow(
            string requiredVersion,
            string installedVersion,
            IEnumerable<string> affectedAssets
        )
        {
            var window = CreateInstance<VersionMismatchWindow>();
            window.titleContent = new GUIContent(Localization.S("version_mismatch.window_title"));
            window.Configure(requiredVersion, installedVersion, affectedAssets);
            window.ShowModalUtility();
        }

        [MenuItem("Tools/Modular Avatar/Debug/Show Version Mismatch Warning")]
        private static void ShowTestWindow()
        {
            ShowWindow(
                "1.18.0",
                "1.17.0",
                TestAssets);
        }

        [MenuItem("Tools/Modular Avatar/Debug/Show Version Mismatch Warning (Non-Modal)")]
        private static void ShowTestWindowNonModal()
        {
            var window = GetWindow<VersionMismatchWindow>(Localization.S("version_mismatch.window_title"));
            window.Configure("1.18.0", "1.17.0", TestAssets);
            window.Show();
        }

        private void CreateGUI()
        {
            var reopenedAfterDomainReload = isConfigured && !configuredThisDomain;
            configuredThisDomain = true;
            Refresh();

            if (reopenedAfterDomainReload)
            {
                EditorApplication.delayCall += RefreshInstalledVersionAfterDomainReload;
            }
        }

        private void Configure(string requiredVersion, string installedVersion, IEnumerable<string> affectedAssets)
        {
            this.requiredVersion = requiredVersion;
            this.installedVersion = installedVersion;
            this.affectedAssets = new List<string>(affectedAssets);
            isConfigured = true;
            configuredThisDomain = true;
            packageManifestContents = ReadPackageManifest();
            minSize = new Vector2(500, 620);
            Refresh();
        }

        private void RefreshInstalledVersionAfterDomainReload()
        {
            if (this == null || !isConfigured)
            {
                return;
            }

            var currentVersion = ReadInstalledVersion();
            if (string.IsNullOrEmpty(currentVersion))
            {
                return;
            }

            installedVersion = currentVersion!;
            packageManifestContents = ReadPackageManifest();

            if (IsInstalledVersionSufficient(installedVersion, requiredVersion))
            {
                Close();
                return;
            }

            Refresh();
        }

        private void RefreshAssetDatabase()
        {
            if (string.Equals(packageManifestContents, ReadPackageManifest(), StringComparison.Ordinal))
            {
                AssetDatabase.Refresh();
                return;
            }

            var pendingRefresh = new PendingRefresh
            {
                requiredVersion = requiredVersion,
                affectedAssets = new List<string>(affectedAssets)
            };
            SessionState.SetString(PendingRefreshKey, JsonUtility.ToJson(pendingRefresh));

            Close();
            EditorApplication.delayCall += () =>
            {
                // Reopening is handled by ReopenAfterDomainReload after compilation finishes.
                AssetDatabase.Refresh();
            };
        }

        private static void ReopenAfterPackageRefresh()
        {
            var serializedRefresh = SessionState.GetString(PendingRefreshKey, "");
            if (string.IsNullOrEmpty(serializedRefresh))
            {
                return;
            }

            SessionState.EraseString(PendingRefreshKey);

            PendingRefresh? pendingRefresh;
            try
            {
                pendingRefresh = JsonUtility.FromJson<PendingRefresh>(serializedRefresh);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (pendingRefresh == null)
            {
                return;
            }

            var currentVersion = ReadInstalledVersion();
            if (currentVersion == null || !IsInstalledVersionSufficient(currentVersion, pendingRefresh.requiredVersion))
            {
                ShowWindow(
                    pendingRefresh.requiredVersion,
                    currentVersion ?? "Unknown",
                    pendingRefresh.affectedAssets
                );
            }
        }

        private static bool IsInstalledVersionSufficient(string currentVersion, string minimumVersion)
        {
            try
            {
                return SemVerComparator.Instance.CompareForCompatibility(currentVersion, minimumVersion) >= 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string? ReadPackageManifest()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(VersionTag).Assembly);
            if (packageInfo == null || string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                return null;
            }

            try
            {
                return File.ReadAllText(Path.Combine(packageInfo.resolvedPath, "package.json"));
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static string? ReadInstalledVersion()
        {
            var manifestContents = ReadPackageManifest();
            if (!string.IsNullOrEmpty(manifestContents))
            {
                try
                {
                    var manifest = JsonUtility.FromJson<PackageManifest>(manifestContents);
                    if (manifest != null && !string.IsNullOrEmpty(manifest.version))
                    {
                        return manifest.version;
                    }
                }
                catch (ArgumentException)
                {
                    // Fall back to the package metadata below.
                }
            }

            return VersionTag.Current.UpdatedAtVersion;
        }

        private static string GetUpgradeGuideUrl()
        {
            var version = ReadInstalledVersion();
            if (version != null && IsPrereleaseVersion(version))
            {
                return DevelopmentUpgradeGuideUrl;
            }

            return UpgradeGuideUrl;
        }

        private static bool IsPrereleaseVersion(string version)
        {
            var buildMetadataIndex = version.IndexOf('+');
            var prereleaseIndex = version.IndexOf('-');
            return prereleaseIndex >= 0 && (buildMetadataIndex < 0 || prereleaseIndex < buildMetadataIndex);
        }

        private void Refresh()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uxml == null || uss == null)
            {
                Debug.LogError("Could not load the Version Mismatch Window UI assets.");
                return;
            }

            rootVisualElement.Clear();
            var root = uxml.CloneTree();
            root.styleSheets.Add(uss);
            rootVisualElement.Add(root);
            Localization.L.LocalizeUIElements(root);

            root.Q<Label>("required-version").text = requiredVersion;
            root.Q<Label>("installed-version").text = installedVersion;
            root.Q<Image>("warning-icon").image = EditorGUIUtility.IconContent("console.warnicon").image;
            root.Q<Button>("upgrade-help").clicked += () => Application.OpenURL(GetUpgradeGuideUrl());
            root.Q<Button>("refresh-asset-database").clicked += RefreshAssetDatabase;
            root.Q<Button>("close-unity").clicked += () => EditorApplication.ExecuteMenuItem("File/Exit");
            root.Q<Button>("ignore").clicked += Close;

            var affectedAssetsList = root.Q<ScrollView>("affected-assets");
            foreach (var asset in affectedAssets)
            {
                var entry = new Label(asset);
                entry.AddToClassList("asset-entry");
                affectedAssetsList.Add(entry);
            }
        }
    }
}