using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;


namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal class PreviewSceneSaveHook : AssetModificationProcessor
    {
        internal static string PreviewScenePath;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (PreviewScenePath == null || !paths.Any(p => p == PreviewScenePath)) return paths;

            return paths.Where(p => p != PreviewScenePath).ToArray();
        }
    }

    internal static class FitPreviewSceneManager
    {
        internal static string PreviewSceneName = "__FitPreviewScene__";
        private const string PreviewSceneGuid = "64d4671a3d1037a43a67356b2e8120ac";

        private static Scene _previewScene;
        
        private static bool _assemblyReloading;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            var clearSceneDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                BindingFlags.NonPublic | BindingFlags.Static);

            EditorApplication.update += () =>
            {
                if (!_previewScene.IsValid()) return;
                if (_previewScene.isDirty)
                    // We never want to save anything in the preview scene, and we definitely don't want to end up with
                    // UI popups prompting to save it, so aggressively clear its dirty flag.
                    clearSceneDirtiness?.Invoke(null, new object[] { _previewScene });

                if (SceneManager.GetActiveScene() == _previewScene)
                {
                    // Oops, make sure the preview scene isn't selected
                    var found = false;

                    var sceneCount = SceneManager.sceneCount;
                    for (var i = 0; i < sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (scene == _previewScene || !scene.isLoaded || !scene.IsValid() || scene.isSubScene) continue;

                        SceneManager.SetActiveScene(scene);
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        // Unload the preview scene if it's the only valid/loaded scene left
                        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);

                        UnloadPreviewScene();
                    }
                }
            };

            // Reset preview scene on play mode transition
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                    UnloadPreviewScene();
            };

            // Reset preview scene on assembly reload
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                UnloadPreviewScene();
                _assemblyReloading = true;
            };

            // Make sure that we never, ever save any gameobjects in the preview scene. If we start saving,
            // destroy them all.
            EditorSceneManager.sceneSaving += (scene, path) =>
            {
                if (scene == _previewScene)
                {
                    foreach (var go in _previewScene.GetRootGameObjects())
                    {
                        if ((go.hideFlags & HideFlags.DontSave) == 0)
                        {
                            Object.DestroyImmediate(go);
                        }
                    }
                }
            };

            Menu.SetChecked("Tools/NDM Framework/Debug Tools/Show Preview Scene", false);
        }

        public static Scene LoadPreviewScene()
        {
            if (_previewScene.IsValid()) return _previewScene;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return default;
            if (_assemblyReloading) return default;

            _previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            if (!_previewScene.IsValid())
            {
                // Load scene from asset
                var assetPath = AssetDatabase.GUIDToAssetPath(PreviewSceneGuid);
                PreviewSceneSaveHook.PreviewScenePath = assetPath;

                _previewScene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
                PreviewSceneName = _previewScene.name;

                // Make sure it's empty, in case the scene file got overwritten somehow
                foreach (var go in _previewScene.GetRootGameObjects()) Object.DestroyImmediate(go);
            }

            return _previewScene;
        }

        internal static void UnloadPreviewScene()
        {
            if (_previewScene.IsValid()) EditorSceneManager.CloseScene(_previewScene, true);
            _previewScene = default;
        }
    }
}