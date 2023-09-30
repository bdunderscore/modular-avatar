using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace modular_avatar_tests.PreserveOutfitPB
{
    public class PreserveOutfitPB : TestBase
    {
        [Test]
        public void NullRootTransform()
        {
            var avatar = CreatePrefab("PreserveOutfitPB_NullRootTransform.prefab");
            AvatarProcessor.ProcessAvatar(avatar);

            var armature = avatar.transform.Find("Armature");
            Assert.That(armature.childCount, Is.EqualTo(2));
            Assert.That(armature.GetChild(0).GetComponent<VRCPhysBone>(), Is.Not.Null);
            Assert.That(armature.GetChild(1).GetComponent<VRCPhysBone>(), Is.Not.Null);
        }

        [Test]
        public void SelfRootTransform()
        {
            var avatar = CreatePrefab("PreserveOutfitPB_SelfRootTransform.prefab");
            AvatarProcessor.ProcessAvatar(avatar);

            var armature = avatar.transform.Find("Armature");
            Assert.That(armature.childCount, Is.EqualTo(2));
            Assert.That(armature.GetChild(0).GetComponent<VRCPhysBone>(), Is.Not.Null);
            Assert.That(armature.GetChild(1).GetComponent<VRCPhysBone>(), Is.Not.Null);
        }

        [Test]
        public void SiblingRootTransform()
        {
            var avatar = CreatePrefab("PreserveOutfitPB_SiblingRootTransform.prefab");
            AvatarProcessor.ProcessAvatar(avatar);

            var armature = avatar.transform.Find("Armature");
            Assert.That(armature.childCount, Is.EqualTo(3));

            var @ref = armature.Find("ref");
            Assert.That(@ref.childCount, Is.EqualTo(1));
            Assert.That(@ref.GetComponent<VRCPhysBone>(), Is.Not.Null);
            Assert.That(@ref.GetChild(0).GetComponent<VRCPhysBone>(), Is.Not.Null);
        }
    }
}