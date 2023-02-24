using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.menu;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    public class ModularAvatarMenuGroup : MenuSourceComponent
    {
        private bool recursing = false;

        public GameObject targetObject;

        public override void Visit(NodeContext context)
        {
            context.PushNode(new MenuNodesUnder(targetObject != null ? targetObject : gameObject));
        }
    }
}