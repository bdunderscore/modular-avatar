using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct MaterialSwitchObject
    {
        public AvatarObjectReference Object;
        public Material Material;
        public int MaterialIndex;

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

    public class ModularAvatarMaterialSetter : ReactiveComponent
    {
        [SerializeField] private AvatarObjectReference m_targetRenderer;

        public AvatarObjectReference targetRenderer
        {
            get => m_targetRenderer;
            set => m_targetRenderer = value;
        }

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
    }
}