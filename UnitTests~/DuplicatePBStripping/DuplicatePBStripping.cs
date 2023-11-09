﻿#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace modular_avatar_tests.DuplicatePBStripping
{
    public class DuplicatePBStripping : TestBase
    {
        [Test]
        public void StripsExtraPBones_withNullRootTransform()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_nullRef.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            Assert.AreEqual(1, prefab.GetComponentsInChildren<VRCPhysBone>().Length);
        }

        [Test]
        public void StripsExtraPBones_withExplicitRootTransform()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_objRef.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            Assert.AreEqual(1, prefab.GetComponentsInChildren<VRCPhysBone>().Length);
        }

        [Test]
        public void StripsExtraPBones_withSiblingRootTransform()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_otherRef.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            Assert.AreEqual(1, prefab.GetComponentsInChildren<VRCPhysBone>().Length);
        }

        [Test]
        public void StripsExtraPBones_notWhenTargetDiffers()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_preserve.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            // Note that this prefab has one duplicate, one non-duplicate component
            Assert.AreEqual(2, prefab.GetComponentsInChildren<VRCPhysBone>().Length);
        }

        [Test]
        public void StripsExtraPBones_near()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_near.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            // Note that this prefab has a pair of PhysBones whose names match and are just 1mm apart.
            // They should be merged because some outfit authors copy the entire armature, including PhysBones.
            Assert.AreEqual(1, prefab.GetComponentsInChildren<VRCPhysBone>().Length);
        }

        [Test]
        public void StripsExtraPBones_far()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_far.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            // Note that this prefab has a pair of PhysBones whose names match but are more than 1mm apart.
            // They should not be merged to preserve intentionally attached PhysBone, which is not copied from the avatar.
            Assert.AreEqual(2, prefab.GetComponentsInChildren<VRCPhysBone>().Length);
        }
    }
}

#endif