using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA MMD Layer Control")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/mmd-layer-control?lang=auto")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // ReSharper disable once RequiredBaseTypesIsNotInherited (false positive)
    public sealed class ModularAvatarMMDLayerControl : StateMachineBehaviour
    {
        [SerializeField] internal bool m_DisableInMMDMode;

        [PublicAPI]
        public bool DisableInMMDMode
        {
            get => m_DisableInMMDMode;
            set => m_DisableInMMDMode = value;
        }
    }
}