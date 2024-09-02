#region

using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
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
            set => m_Scale = value;
        }

        [SerializeField] [FormerlySerializedAs("scaleProxy")]
        internal Transform legacyScaleProxy;
    }
}