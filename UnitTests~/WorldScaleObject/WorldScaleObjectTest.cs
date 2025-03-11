using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace UnitTests.WorldScaleObject
{
    public class WorldScaleObjectTest : TestBase
    {
        [Test]
        public void TestWSO()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var wso = child.AddComponent<ModularAvatarWorldScaleObject>();
            
            AvatarProcessor.ProcessAvatar(root);
            
            Assert.IsTrue(wso == null);
            
            #if MA_VRCSDK3_AVATARS
            AssertVRCScaleConstraintPresent(child);
            #else
            AssertScaleConstraintPresent(child);
            #endif
        }

        #if MA_VRCSDK3_AVATARS
        private void AssertVRCScaleConstraintPresent(GameObject child)
        {
            var scaleConstraint = child.GetComponent<VRCScaleConstraint>();
            Assert.IsNotNull(scaleConstraint);
            Assert.AreEqual(1, scaleConstraint.Sources.Count);
            Assert.AreEqual("FixedPrefab", scaleConstraint.Sources[0].SourceTransform.gameObject.name);
            Assert.AreEqual(1, scaleConstraint.Sources[0].Weight);
            Assert.AreEqual(true, scaleConstraint.Locked);
            Assert.AreEqual(true, scaleConstraint.IsActive);
        }
        #endif

        private void AssertScaleConstraintPresent(GameObject child)
        {
            var scaleConstraint = child.GetComponent<ScaleConstraint>();
            Assert.IsNotNull(scaleConstraint);
            Assert.AreEqual(1, scaleConstraint.sourceCount);
            Assert.AreEqual("FixedPrefab", scaleConstraint.GetSource(0).sourceTransform.gameObject.name);
            Assert.AreEqual(1, scaleConstraint.GetSource(0).weight);
            Assert.AreEqual(true, scaleConstraint.locked);
            Assert.AreEqual(true, scaleConstraint.constraintActive);
        }
    }
}