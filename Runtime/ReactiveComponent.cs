using UnityEngine;
using UnityEngine.Serialization;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    ///     Tag class used internally to mark reactive components. Not publicly extensible.
    /// </summary>
    public abstract class ReactiveComponent : AvatarTagComponent
    {
        [SerializeField] private bool m_inverted = false;
        
        public bool Inverted
        {
            get => m_inverted;
            set => m_inverted = value;
        }

        internal ReactiveComponent()
        {
        }
    }
}