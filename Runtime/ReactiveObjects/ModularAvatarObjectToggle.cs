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
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/object-toggle?lang=auto")]
    public class ModularAvatarObjectToggle : ReactiveComponent
    {
        [SerializeField] private List<ToggledObject> m_objects = new();

        public List<ToggledObject> Objects
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