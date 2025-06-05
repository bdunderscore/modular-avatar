using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct MatSwap
    {
        public Material From;
        public Material To;

        public MatSwap Clone()
        {
            return new()
            {
                From = From,
                To = To,
            };
        }
    }

    [AddComponentMenu("Modular Avatar/MA Material Swap")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/material-swap?lang=auto")]
    public class ModularAvatarMaterialSwap : ReactiveComponent
    {
        [SerializeField]
        private List<MatSwap> m_swaps = new();

        public List<MatSwap> Swaps
        {
            get => m_swaps;
            set => m_swaps = value;
        }
    }
}
