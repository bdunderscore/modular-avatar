#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

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

        [Test]
        public void AcceptsHumanoidPB_OnTipBones()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_HumanoidTip.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var head = prefab.transform.Find("Armature/Hips/Spine/Chest/Neck/Head");
            Transform subHead = null;
            
            foreach (Transform t in head)
            {
                if (t.gameObject.name.StartsWith("Head$"))
                {
                    subHead = t;
                    break;
                }
            }
            
            Assert.NotNull(subHead);
            
            Assert.AreEqual(1, subHead.childCount);
            Assert.IsTrue(subHead.TryGetComponent<VRCPhysBone>(out _));
        }

        [Test]
        public void RejectsHumanoidPB_OnInnerBones()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_HumanoidInner.prefab");

            var context = CreateContext(prefab);
            
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var errors = ErrorReport.CaptureErrors(() => new MergeArmatureHook().OnPreprocessAvatar(context, prefab));
            Assert.AreEqual(1, errors.Count);

            var error = errors[0];
            Assert.AreEqual("error.merge_armature.physbone_on_humanoid_bone", ((SimpleError)error.TheError).TitleKey);
        }

        [Test]
        public void AcceptsHumanoidPB_OnInnerBones_WithPBIgnores()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_HumanoidInner_Ignored.prefab");

            AssertInnerBones(prefab);
        }

        [Test]
        public void AcceptsHumanoidPB_OnInnerBones_WithPBBlocker()
        {
            var prefab = CreatePrefab("DuplicatePBStripping_HumanoidInner_PBBlocker.prefab");

            AssertInnerBones(prefab);
        }

        private static void AssertInnerBones(GameObject prefab)
        {
            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(prefab));
            Assert.AreEqual(0, errors.Count);

            var hips = prefab.transform.Find("Armature/Hips");
            Transform subHips = null;
            foreach (Transform t in hips)
            {
                if (t.gameObject.name.StartsWith("Hips"))
                {
                    subHips = t;
                    break;
                }
            }

            Assert.NotNull(subHips);
            Assert.AreEqual(1, subHips.childCount);
            Assert.IsTrue(subHips.GetChild(0).name.StartsWith("New Child"));
            Assert.IsTrue(subHips.TryGetComponent<VRCPhysBone>(out _));
        }
    }
}

#endif