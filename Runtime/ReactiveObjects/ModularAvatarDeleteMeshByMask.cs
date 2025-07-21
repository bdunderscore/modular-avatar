using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum DeleteMeshByMaskMode
    {
        DontDelete,
        DeleteBlack,
        DeleteWhite,
    }

    [Serializable]
    public class DeleteMeshByMaskObject
    {
        public AvatarObjectReference Object;
        public int MaterialIndex;
        public Texture2D MaskTexture;
        public DeleteMeshByMaskMode DeleteMode;

        public DeleteMeshByMaskObject Clone()
        {
            return new()
            {
                Object = Object.Clone(),
                MaterialIndex = MaterialIndex,
                MaskTexture = MaskTexture,
                DeleteMode = DeleteMode,
            };
        }

        public bool Equals(DeleteMeshByMaskObject other)
        {
            return Equals(Object, other.Object)
                   && MaterialIndex == other.MaterialIndex
                   && MaskTexture == other.MaskTexture
                   && DeleteMode == other.DeleteMode;
        }

        public override bool Equals(object obj)
        {
            return obj is DeleteMeshByMaskObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Object, MaterialIndex, MaskTexture, (int)DeleteMode);
        }
    }

    [AddComponentMenu("Modular Avatar/MA Delete Mesh By Mask")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/delete-mesh-by-mask?lang=auto")]
    public class ModularAvatarDeleteMeshByMask : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField]
        private DeleteMeshByMaskObject m_object = new();

        public DeleteMeshByMaskObject Object
        {
            get => m_object;
            set => m_object = value;
        }

        public override void ResolveReferences()
        {
            m_object.Object?.Get(this);
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (m_object.Object != null)
                yield return m_object.Object;
        }
    }
}
