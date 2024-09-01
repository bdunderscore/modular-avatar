using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace modular_avatar_tests
{
    public class AnimParameterPathRewritingTest : TestBase
    {
        [Test]
        public void test_AnimParameterPathRewriting()
        {
            var prefab = CreatePrefab("AnimParameterPathRewriting.prefab");

            var originalClip = LoadAsset<AnimationClip>("clip.anim");
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            var origCurves = AnimationUtility.GetCurveBindings(originalClip);
            Assert.AreEqual(1, origCurves.Length);
            Assert.AreEqual("", origCurves[0].path);
            Assert.AreEqual(typeof(Animator), origCurves[0].type);
            Assert.AreEqual("p", origCurves[0].propertyName);
            
            var curves = AnimationUtility.GetCurveBindings(findFxClip(prefab, "l1"));
            Assert.AreEqual(1, curves.Length);
            Assert.AreEqual("", curves[0].path);
            Assert.AreEqual(typeof(Animator), curves[0].type);
            Assert.AreEqual("x", curves[0].propertyName);
            
            // Animation override controller handling
            curves = AnimationUtility.GetCurveBindings(findFxClip(prefab, "l2"));
            Assert.AreEqual(1, curves.Length);
            Assert.AreEqual("", curves[0].path);
            Assert.AreEqual(typeof(Animator), curves[0].type);
            Assert.AreEqual("x", curves[0].propertyName);
        }
    }
}