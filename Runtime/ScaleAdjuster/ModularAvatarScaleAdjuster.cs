#region

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                PreCull();
            }
        }

        [SerializeField] [FormerlySerializedAs("scaleProxy")]
        internal Transform legacyScaleProxy;

        internal Transform scaleProxyChild;

        [NonSerialized]
        private bool initialized = false;

#if UNITY_EDITOR
        void Awake()
        {
            ProxyManager.RegisterAdjuster(this);
            initialized = false;
        }

        void OnValidate()
        {
            ProxyManager.RegisterAdjuster(this);
            initialized = false;
        }

        internal void PreCull()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;

            if (scaleProxyChild == null || initialized == false)
            {
                InitializeProxy();
            }

            UpdateProxyParent(scaleProxyChild, transform);

            var xform = transform;
            scaleProxyChild.localScale = m_Scale;

            ProxyManager.RegisterBone(xform, scaleProxyChild);
            
            if (legacyScaleProxy != null && !PrefabUtility.IsPartOfPrefabAsset(legacyScaleProxy))
            {
                DestroyImmediate(legacyScaleProxy.gameObject);
                legacyScaleProxy = null;
            }
        }

        private void UpdateProxyParent(Transform proxyChild, Transform trueParent)
        {
            while (trueParent != null)
            {
                Transform parent = proxyChild.parent;
                if (parent == null)
                {
                    GameObject obj = new GameObject();
                    proxyChild.transform.SetParent(obj.transform, false);
                    #if MODULAR_AVATAR_DEBUG_HIDDEN
                    obj.hideFlags = HideFlags.DontSave;
                    #else
                    obj.hideFlags = HideFlags.HideAndDontSave;
                    #endif
                    parent = obj.transform;
                    
                    if (obj.scene != gameObject.scene && gameObject.scene.IsValid())
                    {
                        SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
                    }
                }

                parent.gameObject.name = "Proxy object for " + trueParent.gameObject.name;
                parent.localPosition = trueParent.localPosition;
                parent.localRotation = trueParent.localRotation;
                parent.localScale = trueParent.localScale;

                proxyChild = parent;
                trueParent = trueParent.parent;
            }

            if (proxyChild.parent != null)
            {
                // Reparent to root
                proxyChild.SetParent(null, false);
                
                // Destroy old hierarchy
                Transform parent = proxyChild.parent;
                while (parent.parent != null) parent = parent.parent;
                DestroyImmediate(parent.gameObject);
            }
        }

        private void InitializeProxy()
        {
            if (scaleProxyChild == null)
            {
                scaleProxyChild = new GameObject("Child").transform;

#if MODULAR_AVATAR_DEBUG_HIDDEN
                scaleProxyChild.gameObject.hideFlags = HideFlags.DontSave;
#else
                scaleProxyChild.gameObject.hideFlags = HideFlags.HideAndDontSave;
#endif
            }
            
            initialized = true;
        }

        private void OnDestroy()
        {
            ProxyManager.UnregisterAdjuster(this);

            if (scaleProxyChild != null)
            {
                Transform parent = scaleProxyChild.parent;
                while (parent.parent != null) parent = parent.parent;
                DestroyImmediate(parent.gameObject);
            }

            if (transform != null)
            {
                ProxyManager.UnregisterBone(transform);
            }

            base.OnDestroy();
        }
#else
        internal void PreCull() {
            // build time stub
        }
#endif
    }
}