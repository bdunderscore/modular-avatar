#region

#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.JacksonDunstan.NativeCollections;
using UnityEngine;
using VRC.SDKBase;
using Object = System.Object;

#endregion

namespace nadena.dev.modular_avatar.core
{
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    internal class ScaleAdjusterRenderer : MonoBehaviour, IEditorOnly
    {
        internal static Dictionary<ScaleAdjusterRenderer, GameObject> originalParent =
            new Dictionary<ScaleAdjusterRenderer, GameObject>(new ObjectIdentityComparer<ScaleAdjusterRenderer>());

        internal static Dictionary<GameObject, GameObject> proxyObjects = new Dictionary<GameObject, GameObject>(
            new ObjectIdentityComparer<GameObject>());

        internal static Dictionary<GameObject, ScaleAdjusterRenderer> originalObjects =
            new Dictionary<GameObject, ScaleAdjusterRenderer>(
                new ObjectIdentityComparer<GameObject>()
            );
        
        private static int RecreateHierarchyIndexCount = 0;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void Setup()
        {
            EditorApplication.hierarchyChanged += InvalidateAll;
        }
#endif

        internal static void InvalidateAll()
        {
            RecreateHierarchyIndexCount++;
        }
        
        private SkinnedMeshRenderer myRenderer;
        private SkinnedMeshRenderer parentRenderer;

        private bool wasActive = false;
        private bool redoBoneMappings = true;
        private bool hasRelevantBones = false;
        private int lastRecreateHierarchyIndex = -1;

        internal Dictionary<Transform, Transform> BoneMappings = new Dictionary<Transform, Transform>(
            new ObjectIdentityComparer<Transform>()
        );
        

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;
            redoBoneMappings = true;

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;

                // We hide this in Harmony, not here, so it is eligible for click-to-select.
                gameObject.hideFlags = HideFlags.DontSaveInBuild;

                if (BoneMappings == null)
                {
                    BoneMappings = new Dictionary<Transform, Transform>();
                }
                
                Configure();
            };

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            redoBoneMappings = true;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                ClearHooks();
            }
        }
#endif

        private Transform MapBone(Transform bone)
        {
            if (bone == null) return null;
            if (BoneMappings.TryGetValue(bone, out var newBone) && newBone != null) return newBone;
            return bone;
        }

        private void OnDestroy()
        {
            ClearHooks();
            #if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            #endif
        }

        private void Configure()
        {
            if (originalParent.TryGetValue(this, out var prevParent) && transform.parent?.gameObject == prevParent)
            {
                return;
            }

            if (prevParent != null)
            {
                ClearHooks();
            }

            if (!hasRelevantBones)
            {
                return;
            }
            
            if (transform.parent == null)
            {
                return;
            }
            
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += ClearHooks;
#endif

            var parent = transform.parent.gameObject;

            proxyObjects[gameObject] = parent;
            originalObjects[parent] = this;
            originalParent[this] = parent;

            CheckBoneUsage(); // register the proxy if needed
        }

        private void ClearHooks()
        {
            if (originalParent.TryGetValue(this, out var prevParent))
            {
                if (parentRenderer != null)
                {
                    CameraHooks.UnregisterProxy(parentRenderer);
                }

                if ((Object)prevParent != null)
                {
                    originalObjects.Remove(prevParent);
                }

                originalParent.Remove(this);
                if (gameObject != null)
                {
                    proxyObjects.Remove(gameObject);
                }
                else
                {
                    CleanDeadObjects();
                }
            }
        }
        
#if UNITY_EDITOR
        private void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (transform.parent == null)
            {
                DestroyImmediate(gameObject);
                return;
            }
            
            if (myRenderer == null)
            {
                myRenderer = GetComponent<SkinnedMeshRenderer>();
            }

            if (parentRenderer == null)
            {
                parentRenderer = transform.parent.GetComponent<SkinnedMeshRenderer>();
            }

            Configure();
            
            myRenderer.sharedMaterials = parentRenderer.sharedMaterials;
            myRenderer.localBounds = parentRenderer.localBounds;
            if (redoBoneMappings || lastRecreateHierarchyIndex != RecreateHierarchyIndexCount
                                 || myRenderer.sharedMesh != parentRenderer.sharedMesh)
            {
                CleanDeadObjects(BoneMappings);

                if (BoneMappings.Count == 0)
                {
                    #if UNITY_2022_3_OR_NEWER
                    DestroyImmediate(gameObject);
                    return;
                    #endif
                }

                myRenderer.sharedMesh = parentRenderer.sharedMesh;
                myRenderer.rootBone = MapBone(parentRenderer.rootBone);
                myRenderer.bones = parentRenderer.bones.Select(MapBone).ToArray();
                redoBoneMappings = false;
                lastRecreateHierarchyIndex = RecreateHierarchyIndexCount;

                CheckBoneUsage();
            }

            if (!hasRelevantBones) return;

            myRenderer.quality = parentRenderer.quality;
            myRenderer.shadowCastingMode = parentRenderer.shadowCastingMode;
            myRenderer.receiveShadows = parentRenderer.receiveShadows;
            myRenderer.lightProbeUsage = parentRenderer.lightProbeUsage;
            myRenderer.reflectionProbeUsage = parentRenderer.reflectionProbeUsage;
            myRenderer.probeAnchor = parentRenderer.probeAnchor;
            myRenderer.motionVectorGenerationMode = parentRenderer.motionVectorGenerationMode;
            myRenderer.allowOcclusionWhenDynamic = parentRenderer.allowOcclusionWhenDynamic;

            if (myRenderer.sharedMesh != null)
            {
                var blendShapeCount = myRenderer.sharedMesh.blendShapeCount;

                for (int i = 0; i < blendShapeCount; i++)
                {
                    myRenderer.SetBlendShapeWeight(i, parentRenderer.GetBlendShapeWeight(i));
                }
            }
        }
#endif
        private void CheckBoneUsage()
        {
            hasRelevantBones = false;
            if (myRenderer.sharedMesh != null)
            {
                var weights = myRenderer.sharedMesh.GetAllBoneWeights();
                var parentBones = parentRenderer.bones;
                foreach (var weight in weights)
                {
                    if (weight.weight < 0.0001f) continue;
                    if (weight.boneIndex < 0 || weight.boneIndex >= parentBones.Length) continue;

                    var bone = parentBones[weight.boneIndex];
                    if (BoneMappings.ContainsKey(bone))
                    {
                        hasRelevantBones = true;
                        break;
                    }
                }
            }

            if (hasRelevantBones)
            {
                CameraHooks.RegisterProxy(parentRenderer, myRenderer);
            }
            else
            {
                CameraHooks.UnregisterProxy(parentRenderer);
                myRenderer.enabled = false;
            }
        }

        public void ClearBoneCache()
        {
            redoBoneMappings = true;
        }

        private static void CleanDeadObjects()
        {
            CleanDeadObjects(originalParent);
            CleanDeadObjects(originalObjects);
            CleanDeadObjects(proxyObjects);
        }

        private static int lastCleanedFrame = 0;
        private static void CleanDeadObjects<K, V>(IDictionary<K, V> dict)
            where K: UnityEngine.Object
            where V: UnityEngine.Object
        {
            // Avoid any O(n^2) behavior if we have lots of cleanup calls happening at the same instant
            if (Time.frameCount == lastCleanedFrame) return;
            lastCleanedFrame = Time.frameCount;
            
            var dead = dict.Where(kvp => kvp.Key == null || kvp.Value == null).ToList();
            
            foreach (var kvp in dead)
            {
                dict.Remove(kvp.Key);
            }
        }
    }
}