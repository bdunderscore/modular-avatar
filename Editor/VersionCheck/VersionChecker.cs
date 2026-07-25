using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace nadena.dev.modular_avatar.editor.version
{
    internal static class VersionChecker
    {
        private static bool IsFired;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditWatcher.Instance.Created += root => CheckRoots(new[] { root });
            EditorSceneManager.sceneOpened += OnSceneLoad;
        }

        private static void OnSceneLoad(Scene scene, OpenSceneMode osm)
        {
            if (osm == OpenSceneMode.AdditiveWithoutLoading) return;
            EditorApplication.delayCall += () =>
            {
                if (scene.IsValid())
                {
                    CheckRoots(scene.GetRootGameObjects());
                }
            };
        }

        private static void CheckRoots(IEnumerable<GameObject> roots)
        {
            if (IsFired) return;

            var requiredVersion = VersionTag.Current.MinimumVersion;
            var flagged = new HashSet<GameObject>();

            var components = roots.SelectMany(root => root.GetComponentsInChildren<AvatarTagComponent>(true));

            foreach (var tagComponent in components)
            {
                var target = tagComponent;
                if (target._modularAvatarVersionTag.IsCompatible) continue;

                // If this is part of a prefab, see if the prefab asset is incompatible as well
                var orig = PrefabUtility.GetCorrespondingObjectFromSource(tagComponent);
                if (orig != null && orig._modularAvatarVersionTag.MinimumVersion ==
                    target._modularAvatarVersionTag.MinimumVersion)
                {
                    target = orig;
                }

                flagged.Add(target.gameObject);

                if (SemVerComparator.Instance.Compare(requiredVersion, target._modularAvatarVersionTag.MinimumVersion) <
                    0)
                {
                    requiredVersion = target._modularAvatarVersionTag.MinimumVersion;
                }
            }

            if (requiredVersion == VersionTag.Current.MinimumVersion)
            {
                return;
            }

            var flaggedAssetList = flagged.Select(obj =>
            {
                if (PrefabUtility.IsPartOfPrefabAsset(obj))
                {
                    var assetRoot = obj.transform.root.gameObject;
                    return "Prefab: " + AssetDatabase.GetAssetPath(assetRoot);
                }

                List<string> pathComponent = new();
                while (obj != null)
                {
                    pathComponent.Add(obj.name);
                    obj = obj.transform.parent?.gameObject;
                }

                pathComponent.Reverse();
                return "Scene: " + string.Join("/", pathComponent);
            }).OrderBy(s => s);

            IsFired = true;
            EditorApplication.delayCall += () =>
            {
                VersionMismatchWindow.ShowWindow(requiredVersion, VersionTag.Current.UpdatedAtVersion,
                    flaggedAssetList);
            };
        }
    }
}