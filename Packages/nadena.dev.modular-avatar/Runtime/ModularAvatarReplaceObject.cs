using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Replace Object")]
    [DisallowMultipleComponent]
    public class ModularAvatarReplaceObject : AvatarTagComponent
    {
        public AvatarObjectReference targetObject = new AvatarObjectReference();
    }
}