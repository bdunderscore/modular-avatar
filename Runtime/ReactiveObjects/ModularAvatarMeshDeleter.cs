using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum MeshDeleteMode
    {
        DontDelete,
        DeleteBlack,
        DeleteWhite,
    }

    [Serializable]
    public class MeshDeleteObject
    {
        public AvatarObjectReference Object;
        public int MaterialIndex;
        public Texture2D MaskTexture;
        public MeshDeleteMode DeleteMode;

        public MeshDeleteObject Clone()
        {
            return new()
            {
                Object = Object.Clone(),
                MaterialIndex = MaterialIndex,
                MaskTexture = MaskTexture,
                DeleteMode = DeleteMode,
            };
        }

        public bool Equals(MeshDeleteObject other)
        {
            return Equals(Object, other.Object)
                   && MaterialIndex == other.MaterialIndex
                   && MaskTexture == other.MaskTexture
                   && DeleteMode == other.DeleteMode;
        }

        public override bool Equals(object obj)
        {
            return obj is MeshDeleteObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Object, MaterialIndex, MaskTexture, (int)DeleteMode);
        }
    }

    [AddComponentMenu("Modular Avatar/MA Mesh Deleter")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/reaction/mesh-deleter?lang=auto")]
    public class ModularAvatarMeshDeleter : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField]
        private List<MeshDeleteObject> m_objects = new();

        public List<MeshDeleteObject> Objects
        {
            get => m_objects;
            set => m_objects = value;
        }

        public override void ResolveReferences()
        {
            foreach (var shape in m_objects)
            {
                shape.Object?.Get(this);
            }
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            foreach (var obj in m_objects)
                if (obj.Object != null)
                    yield return obj.Object;
        }
    }
}
