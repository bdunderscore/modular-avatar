#if MA_VRM1

using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

namespace nadena.dev.modular_avatar.core.vrm
{
    [AddComponentMenu("Modular Avatar/MA Merge VRM1 SpringBones")]
    [DisallowMultipleComponent]
    public class ModularAvatarMergeVRM1SpringBones : AvatarTagComponent
    {
        public List<VRM10SpringBoneColliderGroup> colliderGroups = new List<VRM10SpringBoneColliderGroup>();
        public List<Vrm10InstanceSpringBone.Spring> springs = new List<Vrm10InstanceSpringBone.Spring>();
    }
}

#endif