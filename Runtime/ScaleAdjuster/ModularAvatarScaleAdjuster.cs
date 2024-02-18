#region

using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace nadena.dev.modular_avatar.core
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Scale Adjuster")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/scale-adjuster?lang=auto")]
    public sealed class ModularAvatarScaleAdjuster : AvatarTagComponent
    {
        private const string ADJUSTER_OBJECT = "MA Scale Adjuster Proxy Renderer"; 
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

        [SerializeField] [FormerlySerializedAs("scaleProxy")]
        internal Transform legacyScaleProxy;

        internal Transform scaleProxy;

        private bool initialized = false;

#if UNITY_EDITOR
        void OnValidate()
        {
            base.OnValidate();
            initialized = false;
        }
        
        private void Update()
        {
            if (scaleProxy == null || initialized == false)
            {
                InitializeProxy();
            }

            if (legacyScaleProxy != null && !PrefabUtility.IsPartOfPrefabAsset(legacyScaleProxy))
            {
                DestroyImmediate(legacyScaleProxy.gameObject);
                legacyScaleProxy = null;
            }
            
            scaleProxy.localScale = m_Scale;
        }

        private void InitializeProxy()
        {
            if (scaleProxy == null)
            {
                scaleProxy = new GameObject(gameObject.name + " (Scale Proxy)").transform;
                scaleProxy.SetParent(transform, false);
                scaleProxy.localPosition = Vector3.zero;
                scaleProxy.localRotation = Quaternion.identity;
                scaleProxy.localScale = m_Scale;
                scaleProxy.gameObject.AddComponent<ScaleProxy>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            ConfigureRenderers();

            initialized = true;
        }

        private void OnDestroy()
        {
            if (scaleProxy != null)
            {
                DestroyImmediate(scaleProxy.gameObject);
            }

            ScaleAdjusterRenderer.InvalidateAll();
            base.OnDestroy();
        }


        private void ConfigureRenderers()
        {
            var avatar = RuntimeUtil.FindAvatarInParents(transform);
            if (avatar == null) return;
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.GetComponent<ScaleAdjusterRenderer>() != null) continue;

                var child = smr.transform.Find(ADJUSTER_OBJECT)?.GetComponent<ScaleAdjusterRenderer>();
                if (child == null)
                {
                    var childObj = new GameObject(ADJUSTER_OBJECT);

                    var childSmr = childObj.AddComponent<SkinnedMeshRenderer>();
                    EditorUtility.CopySerialized(smr, childSmr);

                    child = childObj.AddComponent<ScaleAdjusterRenderer>();
                    child.transform.SetParent(smr.transform, false);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                }

                child.BoneMappings[transform] = scaleProxy;
                child.ClearBoneCache();
            }
        }
#endif

#if !UNITY_EDITOR
        private void Update()
        {
            // placeholder to make builds work
        }
#endif
    }
}