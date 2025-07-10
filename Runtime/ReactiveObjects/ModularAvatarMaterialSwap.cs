using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum QuickSwapMode
    {
        None,
        SameDirectory,
        SiblingDirectory,
    }
    
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
    [PublicAPI]
    public class ModularAvatarMaterialSwap : ReactiveComponent, IModularAvatarMaterialChanger, IHaveObjReferences
    {
        [SerializeField]
        private AvatarObjectReference m_root = new();

        [SerializeField]
        internal List<MatSwap> m_swaps = new();
        
        [SerializeField]
        internal QuickSwapMode m_quickSwapMode = QuickSwapMode.None;

        public AvatarObjectReference Root
        {
            get => m_root;
            set => m_root = value;
        }

        public List<MatSwap> Swaps
        {
            get => m_swaps;
            set => m_swaps = value;
        }
        
        public QuickSwapMode QuickSwapMode
        {
            get => m_quickSwapMode;
            set => m_quickSwapMode = value;
        }

        public override void ResolveReferences()
        {
            m_root?.Get(this);
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (m_root != null) yield return m_root;
        }
    }
}
