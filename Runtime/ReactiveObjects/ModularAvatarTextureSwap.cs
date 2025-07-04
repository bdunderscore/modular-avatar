using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct TexSwap
    {
        public Texture From;
        public Texture To;

        public TexSwap Clone()
        {
            return new()
            {
                From = From,
                To = To,
            };
        }
    }

    [AddComponentMenu("Modular Avatar/MA Texture Swap")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/texture-swap?lang=auto")]
    public class ModularAvatarTextureSwap : ReactiveComponent, IModularAvatarMaterialChanger, IHaveObjReferences
    {
        [SerializeField]
        private AvatarObjectReference m_root = new();

        [SerializeField]
        internal List<TexSwap> m_swaps = new();

        public AvatarObjectReference Root
        {
            get => m_root;
            set => m_root = value;
        }

        public List<TexSwap> Swaps
        {
            get => m_swaps;
            set => m_swaps = value;
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
