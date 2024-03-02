using System.Collections.Generic;
using nadena.dev.modular_avatar.JacksonDunstan.NativeCollections;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    internal static class CameraHooks
    {
        private static Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> originalToProxy 
            = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>(
                new ObjectIdentityComparer<SkinnedMeshRenderer>()
            );

        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearStates;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, path) => ClearStates();
        }
        #endif
        
        internal static void RegisterProxy(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy)
        {
            originalToProxy[original] = proxy;
        }
        
        internal static void UnregisterProxy(SkinnedMeshRenderer original)
        {
            originalToProxy.Remove(original);
        }

        private static List<(SkinnedMeshRenderer, bool)> statesToRestore = new List<(SkinnedMeshRenderer, bool)>();

        private static List<SkinnedMeshRenderer> toDeregister = new List<SkinnedMeshRenderer>();

        
        private static void OnPreCull(Camera camera)
        {
            ClearStates();
            toDeregister.Clear();
            
            foreach (var kvp in originalToProxy)
            {
                var original = kvp.Key;
                var proxy = kvp.Value;

                if (original == null || proxy == null)
                {
                    toDeregister.Add(original);
                    continue;
                }

                proxy.enabled = original.enabled;
                if (original.enabled && original.gameObject.activeInHierarchy)
                {
                    statesToRestore.Add((original, original.enabled));
                    original.enabled = false;
                }
            }
            
            foreach (var original in toDeregister)
            {
                originalToProxy.Remove(original);
            }
        }

        private static void OnPostRender(Camera camera)
        {
            ClearStates();
        }


        private static void ClearStates()
        {
            foreach (var (original, state) in statesToRestore)
            {
                original.enabled = state;
            }
            
            statesToRestore.Clear();
        }
    }
}