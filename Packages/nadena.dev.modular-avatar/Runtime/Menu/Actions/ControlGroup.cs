using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Control Group")]
    public class ControlGroup : AvatarTagComponent
    {
        public bool isSynced = true;
        public bool isSaved = true;

        public ModularAvatarMenuItem defaultValue;
    }
}