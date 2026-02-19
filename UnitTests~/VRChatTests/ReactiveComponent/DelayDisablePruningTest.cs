#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.ReactiveComponent
{
    public class DelayDisablePruningTest : TestBase
    {
        [Test]
        public void TestDelayDisablePruning()
        {
            var prefab = CreatePrefab("DelayDisablePruningTest.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            var delayDisable = fxc.layers[^1];
            Assert.AreEqual("Modular Avatar: Responsive Objects Blendtree", delayDisable.name);

            HashSet<string> mentionedPaths = new();
            foreach (var clip in fxc.animationClips)
            {
                if (clip == null) continue;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    mentionedPaths.Add(binding.path);
                }
            }
            
            Assert.IsTrue(mentionedPaths.Contains("shouldRetain"));
            Assert.IsFalse(mentionedPaths.Contains("shouldNotRetain"));
        }
    }
}

#endif