﻿using nadena.dev.modular_avatar.core.menu;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Menu Group")]
    public class ModularAvatarMenuGroup : MenuSourceComponent
    {
        public GameObject targetObject;

        public override void Visit(NodeContext context)
        {
            context.PushNode(new MenuNodesUnder(targetObject != null ? targetObject : gameObject));
        }
    }
}