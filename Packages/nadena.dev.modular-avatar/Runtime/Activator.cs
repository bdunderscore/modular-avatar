#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// This component is used to trigger MA processing upon entering play mode (prior to Av3Emu running).
    /// We create it on a hidden object via AvatarTagObject's OnValidate, and it will proceed to add MAAvatarActivator
    /// components to all avatar roots which contain MA components. This MAAvatarActivator component then performs MA
    /// processing on Awake.
    ///
    /// Note that we do not directly process the avatars from MAActivator. This is to avoid processing avatars that are
    /// initially inactive in the scene (which can have high overhead if the user has a lot of inactive avatars in the
    /// scene).
    /// </summary>
    [AddComponentMenu("")]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-9998)]
    public class Activator : MonoBehaviour
    {
        private const string TAG_OBJECT_NAME = "ModularAvatarInternal_Activator";

        private void Awake()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;

            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>())
                {
                    if (avatar.GetComponentInChildren<AvatarTagComponent>(true) != null)
                    {
                        avatar.gameObject.GetOrAddComponent<AvatarActivator>().hideFlags = HideFlags.HideInInspector;
                    }
                }
            }
        }

        private bool HasMAComponentsInScene()
        {
            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<AvatarTagComponent>(true) != null) return true;
            }

            return false;
        }

        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;

                gameObject.hideFlags = HIDE_FLAGS;
                if (!HasMAComponentsInScene())
                {
                    var scene = gameObject.scene;
                    DestroyImmediate(gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            };
        }

        internal static void CreateIfNotPresent(Scene scene)
        {
            if (!scene.IsValid() || EditorSceneManager.IsPreviewScene(scene)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool rootPresent = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Activator>() != null)
                {
                    root.hideFlags = HIDE_FLAGS;
                    if (rootPresent) DestroyImmediate(root);
                    rootPresent = true;
                }
            }

            if (rootPresent) return;

            var oldActiveScene = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var gameObject = new GameObject(TAG_OBJECT_NAME);
                gameObject.AddComponent<Activator>();
                gameObject.hideFlags = HIDE_FLAGS;
            }
            finally
            {
                SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        private const HideFlags HIDE_FLAGS = HideFlags.HideInHierarchy;
    }

    [AddComponentMenu("")]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-9997)]
    public class AvatarActivator : MonoBehaviour
    {
        private void Awake()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;
            RuntimeUtil.OnDemandProcessAvatar(RuntimeUtil.OnDemandSource.Awake, this);
        }

        private void Start()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;
            RuntimeUtil.OnDemandProcessAvatar(RuntimeUtil.OnDemandSource.Start, this);
        }

        private void Update()
        {
            DestroyImmediate(this);
        }
    }
}
#endif