using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct ToggledObject
    {
        public AvatarObjectReference Object;
        public bool Active;
    }

    [AddComponentMenu("Modular Avatar/MA Object Toggle")]
    public class ModularAvatarObjectToggle : AvatarTagComponent
    {
        [SerializeField] private List<ToggledObject> m_objects = new();

        public List<ToggledObject> Objects
        {
            get => m_objects;
            set => m_objects = value;
        }
    }
}