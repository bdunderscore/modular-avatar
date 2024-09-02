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

        public ToggledObject Clone()
        {
            return new ToggledObject
            {
                Object = Object.Clone(),
                Active = Active
            };
        }
    }

    [AddComponentMenu("Modular Avatar/MA Object Toggle")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/object-toggle?lang=auto")]
    public class ModularAvatarObjectToggle : ReactiveComponent, IHaveObjReferences
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

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            foreach (var obj in m_objects)
                if (obj.Object != null)
                    yield return obj.Object;
        }
    }
}