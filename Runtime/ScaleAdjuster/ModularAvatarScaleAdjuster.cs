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

        internal Transform scaleProxyParent, scaleProxyChild;

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

            if (scaleProxyParent == null || initialized == false)
            {
                InitializeProxy();
            }

            var xform = transform;
            scaleProxyParent.position = transform.position;
            scaleProxyParent.rotation = transform.rotation;
            scaleProxyParent.localScale = transform.localScale;
            scaleProxyChild.localScale = m_Scale;

            ProxyManager.RegisterBone(xform, scaleProxyChild);
            
            if (legacyScaleProxy != null && !PrefabUtility.IsPartOfPrefabAsset(legacyScaleProxy))
            {
                DestroyImmediate(legacyScaleProxy.gameObject);
                legacyScaleProxy = null;
            }
        }

        private void InitializeProxy()
        {
            if (scaleProxyParent == null)
            {
                scaleProxyParent = new GameObject(gameObject.name + " (Scale Proxy)").transform;
                scaleProxyChild = new GameObject("Child").transform;

                scaleProxyChild.transform.SetParent(scaleProxyParent, false);

#if MODULAR_AVATAR_DEBUG_HIDDEN
                scaleProxyParent.gameObject.hideFlags = HideFlags.DontSave;
                scaleProxyChild.gameObject.hideFlags = HideFlags.DontSave;
#else
                scaleProxyParent.gameObject.hideFlags = HideFlags.HideAndDontSave;
                scaleProxyChild.gameObject.hideFlags = HideFlags.HideAndDontSave;
#endif

                if (scaleProxyParent.gameObject.scene != gameObject.scene && gameObject.scene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(scaleProxyParent.gameObject, gameObject.scene);
                }
            }
            
            initialized = true;
        }

        private void OnDestroy()
        {
            ProxyManager.UnregisterAdjuster(this);

            if (scaleProxyParent != null)
            {
                DestroyImmediate(scaleProxyParent.gameObject);
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