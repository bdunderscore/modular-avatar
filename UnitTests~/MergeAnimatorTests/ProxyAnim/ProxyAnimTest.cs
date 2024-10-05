using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.MergeAnimatorTests.ProxyAnim
{
    public class ProxyAnimTest : TestBase
    {
        [Test]
        public void whenProxyAnimIsMerged_itIsNotReplaced()
        {
            var root = CreatePrefab("ProxyAnimTest.prefab");

            var originalAnimator = (AnimatorController) root.GetComponentInChildren<ModularAvatarMergeAnimator>().animator;
            var originalClip = originalAnimator.layers[0].stateMachine.states[0].state.motion as AnimationClip;
            
            AvatarProcessor.ProcessAvatar(root);
            
            var resultLayer = findFxLayer(root, "proxy");
            var resultClip = resultLayer.stateMachine.states[0].state.motion as AnimationClip;
            
            Assert.AreEqual(originalClip, resultClip);
        }
    }
}