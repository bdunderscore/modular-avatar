#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.core.menu;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Menu Group")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/menu-group?lang=auto")]
    public class ModularAvatarMenuGroup : MenuSourceComponent
    {
        public GameObject targetObject;

        public override void Visit(NodeContext context)
        {
            context.PushNode(new MenuNodesUnder(targetObject != null ? targetObject : gameObject));
        }

        public override void ResolveReferences()
        {
            // no-op
        }
    }
}

#endif