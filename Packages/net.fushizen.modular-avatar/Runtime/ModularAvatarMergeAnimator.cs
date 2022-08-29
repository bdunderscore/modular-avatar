using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.modular_avatar.core
{
    public class ModularAvatarMergeAnimator : AvatarTagComponent
    {
        public RuntimeAnimatorController animator;
        public VRCAvatarDescriptor.AnimLayerType layerType;
        public bool deleteAttachedAnimator;
    }
}