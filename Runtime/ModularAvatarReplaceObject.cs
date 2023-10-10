using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Replace Object")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/replace-object")]
    public class ModularAvatarReplaceObject : AvatarTagComponent
    {
        public AvatarObjectReference targetObject = new AvatarObjectReference();

        public override void ResolveReferences()
        {
            targetObject?.Get(this);
        }
    }
}