#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDKBase;

#endregion

namespace nadena.dev.modular_avatar.core
{
    [ExecuteInEditMode]
    //[AddComponentMenu("")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    internal class ScaleAdjusterRenderer : MonoBehaviour, IEditorOnly
    {
        private static event Action OnClearAllOverrides;
        private static int RecreateHierarchyIndexCount = 0;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void Setup()
        {
            UnityEditor.EditorApplication.hierarchyChanged += InvalidateAll;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearAllOverrides;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, path) => ClearAllOverrides();
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
        private int lastRecreateHierarchyIndex = -1;

        internal Dictionary<Transform, Transform> BoneMappings = new Dictionary<Transform, Transform>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
            redoBoneMappings = true;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;

#if MODULAR_AVATAR_DEBUG_HIDDEN
                gameObject.hideFlags = HideFlags.None;
#else
                gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;
#endif
                if (BoneMappings == null)
                {
                    BoneMappings = new Dictionary<Transform, Transform>();
                }
            };
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
            ClearAllOverrides();
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (myRenderer == null)
            {
                myRenderer = GetComponent<SkinnedMeshRenderer>();
            }

            if (parentRenderer == null)
            {
                parentRenderer = transform.parent.GetComponent<SkinnedMeshRenderer>();
            }

            myRenderer.sharedMaterials = parentRenderer.sharedMaterials;
            myRenderer.sharedMesh = parentRenderer.sharedMesh;
            myRenderer.localBounds = parentRenderer.localBounds;
            if (redoBoneMappings || lastRecreateHierarchyIndex != RecreateHierarchyIndexCount)
            {
                var deadBones = BoneMappings.Keys.Where(k => BoneMappings[k] == null)
                    .ToList();
                deadBones.ForEach(k => { BoneMappings.Remove(k); });

                if (BoneMappings.Count == 0)
                {
                    DestroyImmediate(gameObject);
                    return;
                }

                myRenderer.rootBone = MapBone(parentRenderer.rootBone);
                myRenderer.bones = parentRenderer.bones.Select(MapBone).ToArray();
                redoBoneMappings = false;
                lastRecreateHierarchyIndex = RecreateHierarchyIndexCount;
            }

            myRenderer.quality = parentRenderer.quality;
            myRenderer.shadowCastingMode = parentRenderer.shadowCastingMode;
            myRenderer.receiveShadows = parentRenderer.receiveShadows;
            myRenderer.lightProbeUsage = parentRenderer.lightProbeUsage;
            myRenderer.reflectionProbeUsage = parentRenderer.reflectionProbeUsage;
            myRenderer.probeAnchor = parentRenderer.probeAnchor;
            myRenderer.motionVectorGenerationMode = parentRenderer.motionVectorGenerationMode;
            myRenderer.allowOcclusionWhenDynamic = parentRenderer.allowOcclusionWhenDynamic;

            var blendShapeCount = myRenderer.sharedMesh.blendShapeCount;

            for (int i = 0; i < blendShapeCount; i++)
            {
                myRenderer.SetBlendShapeWeight(i, parentRenderer.GetBlendShapeWeight(i));
            }

            ClearAllOverrides();

            myRenderer.enabled = parentRenderer.enabled;
        }

        public void OnWillRenderObject()
        {
            if (myRenderer == null || parentRenderer == null)
            {
                return;
            }

            ClearAllOverrides();

            if (!parentRenderer.enabled || !parentRenderer.gameObject.activeInHierarchy)
            {
                return;
            }

            parentRenderer.enabled = false;
            wasActive = true;
            var objName = parentRenderer.gameObject.name;
            OnClearAllOverrides += ClearLocalOverride;
            // Sometimes - e.g. around domain reloads or undo operations - the parent renderer's enabled field might get
            // re-disabled; re-enabler it in delayCall in this case.
            UnityEditor.EditorApplication.delayCall += ClearLocalOverride;
        }
#endif

        private void ClearLocalOverride()
        {
            if (parentRenderer != null)
            {
                parentRenderer.enabled = true;
            }
        }

        private void OnPostRender()
        {
            ClearAllOverrides();
        }

        public void ClearBoneCache()
        {
            redoBoneMappings = true;
        }

        internal static void ClearAllOverrides()
        {
            OnClearAllOverrides?.Invoke();
            OnClearAllOverrides = null;
        }
    }
}