using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Control Group")]
    public class ControlGroup : ActionController
    {
        public bool isSynced = true;
        public bool isSaved = true;

        public ModularAvatarMenuItem defaultValue;

        internal override bool isSyncedProp => isSynced;
        internal override bool isSavedProp => isSaved;
    }
}