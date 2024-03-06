#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.JacksonDunstan.NativeCollections;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace nadena.dev.modular_avatar.core
{
    internal static class ProxyManager
    {
        #region Accessible from multiple threads

        private static bool _dirty = false;
        private static readonly object _lock = new object();

        private static ImmutableHashSet<ModularAvatarScaleAdjuster> _adjusters
            = ImmutableHashSet<ModularAvatarScaleAdjuster>.Empty;

        private static ImmutableDictionary<Transform, Transform> _originalToReplacementBone
            = ImmutableDictionary<Transform, Transform>.Empty.WithComparers(new ObjectIdentityComparer<Transform>());

        internal static void RegisterAdjuster(ModularAvatarScaleAdjuster adjuster)
        {
            lock (_lock)
            {
                _adjusters = _adjusters.Add(adjuster);
                _dirty = true;
            }
        }

        internal static void UnregisterAdjuster(ModularAvatarScaleAdjuster adjuster)
        {
            lock (_lock)
            {
                _adjusters = _adjusters.Remove(adjuster);
                _dirty = true;
            }
        }

        internal static void RegisterBone(Transform original, Transform replacement)
        {
            lock (_lock)
            {
                if (_originalToReplacementBone.TryGetValue(original, out var val) && val == replacement)
                {
                    return;
                }

                _originalToReplacementBone = _originalToReplacementBone.Add(original, replacement);
                _dirty = true;
            }
        }

        internal static void UnregisterBone(Transform original)
        {
            lock (_lock)
            {
                _originalToReplacementBone = _originalToReplacementBone.Remove(original);
                _dirty = true;
            }
        }

        #endregion

        private static ImmutableHashSet<ModularAvatarScaleAdjuster> _capturedAdjusters =
            ImmutableHashSet<ModularAvatarScaleAdjuster>.Empty;

        private static ImmutableDictionary<Transform, Transform> _capturedBones =
            ImmutableDictionary<Transform, Transform>.Empty;

        private static ImmutableDictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> _originalToReplacementRenderer
            = ImmutableDictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>.Empty.WithComparers(
                new ObjectIdentityComparer<SkinnedMeshRenderer>());

        internal static ImmutableDictionary<GameObject, GameObject> ProxyToOriginalObject { get; private set; } =
            ImmutableDictionary<GameObject, GameObject>.Empty;

        internal static ImmutableDictionary<GameObject, GameObject> OriginalToProxyObject { get; private set; } =
            ImmutableDictionary<GameObject, GameObject>.Empty;

        internal static ImmutableDictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> OriginalToProxyRenderer =>
            _originalToReplacementRenderer;

        internal static ImmutableHashSet<GameObject> RetainedObjects;

        internal static bool ShouldRetain(GameObject obj) => RetainedObjects.Contains(obj);

        private static void BuildRenderers()
        {
            lock (_lock)
            {
                _capturedAdjusters = _adjusters;

                // Give each adjuster a chance to initialize the bone mappings first
                foreach (var adj in _capturedAdjusters)
                {
                    adj.PreCull();
                }

                foreach (var kvp in _originalToReplacementBone)
                {
                    if (kvp.Key == null || kvp.Value == null)
                    {
                        _originalToReplacementBone = _originalToReplacementBone.Remove(kvp.Key);
                    }
                }

                _capturedBones = _originalToReplacementBone;
                _dirty = false;
            }

            var avatarRoots = _capturedBones.Keys.Select(RuntimeUtil.FindAvatarTransformInParents).ToImmutableHashSet();
            var potentialRenderers = avatarRoots.SelectMany(r => r.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                .ToList();

            ImmutableDictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>.Builder renderers =
                ImmutableDictionary.CreateBuilder<SkinnedMeshRenderer, SkinnedMeshRenderer>(
                new ObjectIdentityComparer<SkinnedMeshRenderer>()
            );

            foreach (var originalRenderer in potentialRenderers)
            {
                SkinnedMeshRenderer replacement;

                if (!NeedsReplacement(originalRenderer))
                {
                    if (_originalToReplacementRenderer.TryGetValue(originalRenderer, out replacement) &&
                        replacement != null)
                    {
                        Object.DestroyImmediate(replacement.gameObject);
                    }

                    continue;
                }

                if (!_originalToReplacementRenderer.TryGetValue(originalRenderer, out replacement) ||
                    replacement == null)
                {
                    replacement = CreateReplacement(originalRenderer);
                }

                SetupBoneMappings(originalRenderer, replacement);

                renderers.Add(originalRenderer, replacement);
            }

            foreach (var kvp in _originalToReplacementRenderer)
            {
                if (!renderers.ContainsKey(kvp.Key))
                {
                    if (kvp.Value != null)
                    {
                        Object.DestroyImmediate(kvp.Value.gameObject);
                    }
                }
            }

            _originalToReplacementRenderer = renderers.ToImmutable();
            ProxyToOriginalObject = _originalToReplacementRenderer.ToImmutableDictionary(
                kvp => kvp.Value.gameObject,
                kvp => kvp.Key.gameObject
            );

            OriginalToProxyObject = _originalToReplacementRenderer.ToImmutableDictionary(
                kvp => kvp.Key.gameObject,
                kvp => kvp.Value.gameObject
            );
            
            RetainedObjects = ProxyToOriginalObject.Keys.Concat(
                _capturedBones.Values.Where(b => b != null).Select(b => b.gameObject)
            ).ToImmutableHashSet(new ObjectIdentityComparer<GameObject>());
        }

        private static void SetupBoneMappings(SkinnedMeshRenderer originalRenderer, SkinnedMeshRenderer replacement)
        {
            replacement.sharedMesh = originalRenderer.sharedMesh;
            replacement.bones = originalRenderer.bones.Select(MapBone).ToArray();
        }

        private static Transform MapBone(Transform srcBone)
        {
            if (_capturedBones.TryGetValue(srcBone, out var newBone) && newBone != null)
            {
                return newBone;
            }
            else
            {
                return srcBone;
            }
        }

        private static SkinnedMeshRenderer CreateReplacement(SkinnedMeshRenderer originalRenderer)
        {
            var obj = new GameObject("MA Proxy Renderer for " + originalRenderer.gameObject.name);
            // We can't use HideAndDontSave as this would break scene view click-to-pick handling
            // (so instead this is hidden via the HierarchyViewPatches harmony hack)
            obj.hideFlags = HideFlags.DontSave;

            var renderer = obj.AddComponent<SkinnedMeshRenderer>();

            return renderer;
        }

        private static bool NeedsReplacement(SkinnedMeshRenderer originalRenderer)
        {
            if (originalRenderer.sharedMesh == null) return false;

            var bones = originalRenderer.bones;
            var weights = originalRenderer.sharedMesh.GetAllBoneWeights();

            for (var i = 0; i < weights.Length; i++)
            {
                var bone = bones[weights[i].boneIndex];
                if (_capturedBones.ContainsKey(bone)) return true;
            }

            return false;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void Init()
        {
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                ClearStates();
                foreach (var renderer in _originalToReplacementRenderer.Values)
                {
                    Object.DestroyImmediate(renderer.gameObject);
                }
            };
            EditorSceneManager.sceneSaving += (scene, path) => ClearStates();
        } 

        private static List<(SkinnedMeshRenderer, bool)> statesToRestore = new List<(SkinnedMeshRenderer, bool)>();
        
        private static void OnPreCull(Camera camera)
        {
            if (_dirty)
            {
                BuildRenderers();
            }
            
            ClearStates();

            foreach (var adj in _capturedAdjusters)
            {
                adj.PreCull(); // update scale
            }

            foreach (var kvp in _originalToReplacementRenderer)
            {
                var original = kvp.Key;
                var proxy = kvp.Value;

                if (original == null || proxy == null)
                {
                    _dirty = true;
                    continue;
                }

                var originalGameObject = original.gameObject;
                var proxyActive = original.enabled && originalGameObject.activeInHierarchy &&
                                  !SceneVisibilityManager.instance.IsHidden(originalGameObject, false);

                proxy.enabled = proxyActive;
                if (original.enabled && originalGameObject.activeInHierarchy)
                {
                    CopyRendererStates(original, proxy);

                    statesToRestore.Add((original, original.enabled));
                    original.forceRenderingOff = true;
                }
            }
        }

        private static void CopyRendererStates(SkinnedMeshRenderer parentRenderer, SkinnedMeshRenderer myRenderer)
        {
            myRenderer.transform.position = parentRenderer.transform.position;
            myRenderer.transform.rotation = parentRenderer.transform.rotation;

            myRenderer.sharedMaterials = parentRenderer.sharedMaterials;
            myRenderer.localBounds = parentRenderer.localBounds;
            myRenderer.rootBone = MapBone(parentRenderer.rootBone);
            myRenderer.quality = parentRenderer.quality;
            myRenderer.shadowCastingMode = parentRenderer.shadowCastingMode;
            myRenderer.receiveShadows = parentRenderer.receiveShadows;
            myRenderer.lightProbeUsage = parentRenderer.lightProbeUsage;
            myRenderer.reflectionProbeUsage = parentRenderer.reflectionProbeUsage;
            myRenderer.probeAnchor = parentRenderer.probeAnchor;
            myRenderer.motionVectorGenerationMode = parentRenderer.motionVectorGenerationMode;
            myRenderer.allowOcclusionWhenDynamic = parentRenderer.allowOcclusionWhenDynamic;

            if (myRenderer.gameObject.scene != parentRenderer.gameObject.scene &&
                parentRenderer.gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(myRenderer.gameObject, parentRenderer.gameObject.scene);
            }

            if (myRenderer.sharedMesh != null)
            {
                var blendShapeCount = myRenderer.sharedMesh.blendShapeCount;

                for (int i = 0; i < blendShapeCount; i++)
                {
                    myRenderer.SetBlendShapeWeight(i, parentRenderer.GetBlendShapeWeight(i));
                }
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
                original.forceRenderingOff = false;
            }
            
            statesToRestore.Clear();
        }
#endif
    }
}