#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.editor.version
{
    internal sealed class ResetVersionInformationWindow : EditorWindow
    {
        private const string RootPath = "Packages/nadena.dev.modular-avatar/Editor/VersionCheck/";
        private const string UxmlPath = RootPath + "ResetVersionInformationWindow.uxml";
        private const string UssPath = RootPath + "ResetVersionInformationWindow.uss";

        [SerializeField] private List<GameObject> sceneObjects = new();
        [SerializeField] private List<string> prefabPaths = new();
        [SerializeField] private string currentVersion = "Unknown";
        [SerializeField] private string? highestRequiredVersion;

        [NonSerialized] private bool initialHeightMeasured;

        internal static void ShowWindow(IEnumerable<GameObject> sceneObjects, IEnumerable<string> prefabPaths)
        {
            var window = CreateInstance<ResetVersionInformationWindow>();
            window.titleContent = new GUIContent(Localization.S("version_reset.window_title"));
            window.sceneObjects = sceneObjects.Where(go => go != null).Distinct().ToList();
            window.prefabPaths = prefabPaths.Distinct().ToList();
            window.currentVersion = VersionTag.Current.UpdatedAtVersion ?? "Unknown";
            window.highestRequiredVersion = FindHighestRequiredVersion(window.sceneObjects, window.prefabPaths);
            window.minSize = new Vector2(450, 0);
            window.ShowModalUtility();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uxml == null || uss == null)
            {
                Debug.LogError("Could not load the Reset Version Information window UI assets.");
                return;
            }

            var root = uxml.CloneTree();
            root.styleSheets.Add(uss);
            rootVisualElement.Add(root);
            Localization.L.LocalizeUIElements(root);

            root.Q<VisualElement>("root-box").RegisterCallback<GeometryChangedEvent>(SetInitialHeight);
            root.Q<Label>("current-version").text = currentVersion;
            root.Q<Label>("highest-required-version").text =
                highestRequiredVersion ?? Localization.S("version_reset.none");

            root.Q<Button>("set-current").clicked += SetRequiredVersionToCurrent;
            root.Q<Button>("clear-required-version").clicked += ClearRequiredVersion;
            root.Q<Button>("cancel").clicked += Close;
        }

        private void SetInitialHeight(GeometryChangedEvent changeEvent)
        {
            if (initialHeightMeasured || changeEvent.newRect.height <= 0)
            {
                return;
            }

            initialHeightMeasured = true;

            var windowChromeHeight = position.height - rootVisualElement.layout.height;
            var measuredHeight = changeEvent.newRect.height + windowChromeHeight;
            minSize = new Vector2(minSize.x, measuredHeight);
            position = new Rect(position.position, new Vector2(position.width, measuredHeight));
        }

        private static string? FindHighestRequiredVersion(
            IEnumerable<GameObject> sceneObjects,
            IEnumerable<string> prefabPaths
        )
        {
            string? highestVersion = null;

            foreach (var sceneObject in sceneObjects)
            {
                FindHighestRequiredVersion(sceneObject, ref highestVersion);
            }

            foreach (var prefabPath in prefabPaths)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    FindHighestRequiredVersion(prefabRoot, ref highestVersion);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return highestVersion;
        }

        private static void FindHighestRequiredVersion(GameObject root, ref string? highestVersion)
        {
            foreach (var component in root.GetComponentsInChildren<AvatarTagComponent>(true))
            {
                var requiredVersion = component._modularAvatarVersionTag.MinimumVersion;
                if (string.IsNullOrWhiteSpace(requiredVersion))
                {
                    continue;
                }

                if (highestVersion == null || SemVerComparator.Instance.Compare(highestVersion, requiredVersion) < 0)
                {
                    highestVersion = requiredVersion;
                }
            }
        }

        private void SetRequiredVersionToCurrent()
        {
            ApplyReset(VersionRecorder.ForceUpdateVersionInformation);
        }

        private void ClearRequiredVersion()
        {
            ApplyReset(VersionRecorder.ClearMinimumVersionInformation);
        }

        private void ApplyReset(Action<GameObject> resetVersionInformation)
        {
            foreach (var sceneObject in sceneObjects.Where(go => go != null))
            {
                Undo.RegisterFullObjectHierarchyUndo(sceneObject, "Reset Modular Avatar version information");
                resetVersionInformation(sceneObject);
            }

            foreach (var prefabPath in prefabPaths)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    resetVersionInformation(prefabRoot);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            Close();
        }
    }

    internal static class ResetVersionInformationMenuItems
    {
        [MenuItem("GameObject/Modular Avatar/Reset version information", true)]
        private static bool ValidateResetSceneObjects()
        {
            return Selection.gameObjects.Any(gameObject =>
                gameObject != null && !EditorUtility.IsPersistent(gameObject));
        }

        [MenuItem("GameObject/Modular Avatar/Reset version information")]
        private static void ResetSceneObjects()
        {
            ResetVersionInformationWindow.ShowWindow(
                Selection.gameObjects.Where(gameObject =>
                    gameObject != null && !EditorUtility.IsPersistent(gameObject)),
                Enumerable.Empty<string>()
            );
        }

        [MenuItem("Assets/Modular Avatar/Reset version information", true)]
        private static bool ValidateResetPrefabAssets()
        {
            return SelectedPrefabPaths().Any();
        }

        [MenuItem("Assets/Modular Avatar/Reset version information")]
        private static void ResetPrefabAssets()
        {
            ResetVersionInformationWindow.ShowWindow(Enumerable.Empty<GameObject>(), SelectedPrefabPaths());
        }

        private static IEnumerable<string> SelectedPrefabPaths()
        {
            return Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) !=
                               PrefabAssetType.NotAPrefab);
        }
    }
}