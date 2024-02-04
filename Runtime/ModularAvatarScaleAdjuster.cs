using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    internal struct ScalePatch
    {
        public SkinnedMeshRenderer smr;
        public int boneIndex;

        public ScalePatch(SkinnedMeshRenderer smr, int boneIndex)
        {
            this.smr = smr;
            this.boneIndex = boneIndex;
        }

        public bool Equals(ScalePatch other)
        {
            return smr.Equals(other.smr) && boneIndex == other.boneIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is ScalePatch other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (smr.GetHashCode() * 397) ^ boneIndex;
            }
        }
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Scale Adjuster")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/scale-adjuster?lang=auto")]
    public sealed class ModularAvatarScaleAdjuster : AvatarTagComponent
    {
        [SerializeField] private Vector3 m_Scale = Vector3.one;

        public Vector3 Scale
        {
            get => m_Scale;
            set
            {
                m_Scale = value;
                Update();
            }
        }

        [SerializeField] internal Transform scaleProxy;

        [SerializeField] private List<ScalePatch> patches = new List<ScalePatch>();

        private bool initialized = false;

#if UNITY_EDITOR
        private void Update()
        {
            if (this == null) return;

            PatchRenderers();

            scaleProxy.localScale = m_Scale;
        }

        void OnValidate()
        {
            initialized = false;
            EditorApplication.delayCall += Update;
        }

        private void PatchRenderers()
        {
            if (initialized || this == null) return;

            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                // Ensure we're using the same ScaleProxy as the corresponding prefab asset.
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(this);
                if (this.scaleProxy == null || prefab.scaleProxy == null || prefab.scaleProxy !=
                    PrefabUtility.GetCorrespondingObjectFromSource(this.scaleProxy))
                {
                    if (prefab.scaleProxy == null && scaleProxy != null)
                    {
                        // Push our ScaleProxy down into the prefab (this happens after applying the ScaleAdjuster
                        // component to a prefab)
                        var assetPath = AssetDatabase.GetAssetPath(prefab);
                        PrefabUtility.ApplyAddedGameObject(scaleProxy.gameObject, assetPath,
                            InteractionMode.AutomatedAction);
                        prefab.scaleProxy = PrefabUtility.GetCorrespondingObjectFromSource(this.scaleProxy);
                    }
                    else
                    {
                        // Clear any duplicate scaleProxy we have

                        if (scaleProxy != null) DestroyImmediate(scaleProxy.gameObject);
                    }

                    var so = new SerializedObject(this);
                    var sp = so.FindProperty(nameof(scaleProxy));
                    PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // Find the corresponding child
                    foreach (Transform t in transform)
                    {
                        if (PrefabUtility.GetCorrespondingObjectFromSource(t) == prefab.scaleProxy)
                        {
                            scaleProxy = t;
                            break;
                        }
                    }
                }
            }

            if (scaleProxy == null && !PrefabUtility.IsPartOfPrefabAsset(this))
            {
                scaleProxy = new GameObject(gameObject.name + " (Scale Proxy)").transform;
                scaleProxy.SetParent(transform, false);
                scaleProxy.localPosition = Vector3.zero;
                scaleProxy.localRotation = Quaternion.identity;
                scaleProxy.localScale = m_Scale;
                scaleProxy.gameObject.AddComponent<ScaleProxy>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            if (scaleProxy != null)
            {
                scaleProxy.hideFlags = HideFlags.HideInHierarchy;

                RewriteBoneReferences(transform, scaleProxy);
            }

            initialized = true;
        }

        private void RewriteBoneReferences(Transform oldBone, Transform newBone, Transform selfTransform = null)
        {
            if (selfTransform == null) selfTransform = transform;

            var prefabNewBone = PrefabUtility.GetCorrespondingObjectFromSource(newBone);

            var oldPatches = new HashSet<ScalePatch>(this.patches);
            var newPatches = new HashSet<ScalePatch>();
            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(selfTransform);

            if (avatarRoot != null)
            {
                foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var serializedObject = new SerializedObject(smr);
                    var bonesArray = serializedObject.FindProperty("m_Bones");
                    int boneCount = bonesArray.arraySize;

                    var parentSmr = PrefabUtility.GetCorrespondingObjectFromSource(smr);
                    var parentBones = parentSmr != null ? parentSmr.bones : null;
                    var propMods = PrefabUtility.GetPropertyModifications(smr);

                    bool changed = false;

                    for (int i = 0; i < boneCount; i++)
                    {
                        var boneProp = bonesArray.GetArrayElementAtIndex(i);
                        var bone = boneProp.objectReferenceValue as Transform;
                        if (bone == oldBone || bone == newBone ||
                            (bone == null && oldPatches.Contains(new ScalePatch(smr, i))))
                        {
                            if (parentBones != null && parentBones[i] == prefabNewBone)
                            {
                                // Remove any prefab overrides for this bone entry
                                changed = boneProp.objectReferenceValue != newBone;
                                boneProp.objectReferenceValue = newBone;
                                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                                PrefabUtility.RevertPropertyOverride(boneProp, InteractionMode.AutomatedAction);
                            }
                            else
                            {
                                boneProp.objectReferenceValue = newBone;
                                changed = true;
                            }

                            newPatches.Add(new ScalePatch(smr, i));
                        }
                    }

                    if (changed)
                    {
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();

                        ConfigurePrefab();
                    }
                }

                if (this != null && newPatches != oldPatches)
                {
                    this.patches = newPatches.ToList();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                }
            }
        }

        private void ConfigurePrefab()
        {
            if (this == null || !PrefabUtility.IsPartOfPrefabInstance(this)) return;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this);
            var path = AssetDatabase.GetAssetPath(source);
            var root = PrefabUtility.LoadPrefabContents(path);

            foreach (var obj in root.GetComponentsInChildren<ModularAvatarScaleAdjuster>())
            {
                obj.PatchRenderers();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            initialized = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            UnpatchRenderers();
        }

        private void UnpatchRenderers()
        {
            var scaleProxy2 = this.scaleProxy;
            var transform2 = this.transform;

            EditorApplication.delayCall += () =>
            {
                if (scaleProxy2 == null) return;

                if (transform2 != null)
                {
                    RewriteBoneReferences(scaleProxy2, transform2, transform2);
                }

                try
                {
                    DestroyImmediate(scaleProxy2.gameObject);
                }
                catch (InvalidOperationException e)
                {
                    // not supported in Unity 2019...
                }
            };
        }
#else
        private void Update()
        {
            // placeholder to make builds work
        }
#endif
    }
}