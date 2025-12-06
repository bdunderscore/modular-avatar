#if MA_VRCSDK3_AVATARS

using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace modular_avatar_tests.ConvertConstraint
{
    internal class ConvertConstraintAnimationTest : TestBase
    {
        [Test]
        public void WhenReplacedObjectIsConverted_AnimationIsAdjusted()
        {
            var prefab = CreatePrefab("WhenReplacedObjectIsConverted_AnimationIsAdjusted.prefab");

            AvatarProcessor.ProcessAvatar(prefab,
                PlatformRegistry.PlatformProviders[WellKnownPlatforms.VRChatAvatar30]
            );

            var motion = (AnimationClip) findFxMotion(prefab, "test");
            var ecb = AnimationUtility.GetCurveBindings(motion)[0];
            
            Assert.AreEqual(typeof(VRCAimConstraint), ecb.type);
        }
    }
}

#endif