using System.Linq;
using modular_avatar_tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.ScaleAdjusterTests
{
    public class ScaleAdjusterTests : TestBase
    {
        [Test]
        public void ScaleAdjuster_FixesHumanAvatarDescription()
        {
            var prefab = CreatePrefab("ScaleAdjuster_FixesHumanAvatarDescription.prefab");

            AvatarProcessor.ProcessAvatar(prefab);

            var animator = prefab.GetComponent<Animator>();
            var humanDesc = animator.avatar.humanDescription;

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var headDesc = humanDesc.skeleton.First(b => b.name == head.gameObject.name);
            
            Assert.That(Vector3.Distance(headDesc.position, head.localPosition), Is.LessThan(0.001f));
        }
    }
}