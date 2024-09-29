using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public class MaterialSwitchObject
    {
        public AvatarObjectReference Object;
        public Material Material;
        public int MaterialIndex;

        public MaterialSwitchObject Clone()
        {
            return new MaterialSwitchObject
            {
                Object = Object.Clone(),
                Material = Material,
                MaterialIndex = MaterialIndex
            };
        }
        
        public bool Equals(MaterialSwitchObject other)
        {
            return Equals(Object, other.Object) && Equals(Material, other.Material) && MaterialIndex == other.MaterialIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialSwitchObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Object, Material, MaterialIndex);
        }
    }
    
    [AddComponentMenu("Modular Avatar/MA Material Setter")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/material-setter?lang=auto")]
    public class ModularAvatarMaterialSetter : ReactiveComponent, IHaveObjReferences
    {
        [SerializeField] private List<MaterialSwitchObject> m_objects = new();
        
        public List<MaterialSwitchObject> Objects
        {
            get => m_objects;
            set => m_objects = value;
        }

        public override void ResolveReferences()
        {
            foreach (var obj in m_objects)
            {
                obj.Object?.Get(this);
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