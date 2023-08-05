using System.Linq;
using modular_avatar_tests;
using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace _ModularAvatar.EditModeTests.SerializationTests
{
    public class SerializationTests : TestBase
    {
        [Test]
        public void SerializationSavesAssetsRecursively()
        {
            var avatar = CreatePrefab("TestSerializationTraversal.prefab");

            var originalAnimController = avatar.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .First(l => l.animatorController != null)
                .animatorController as AnimatorController;

            AvatarProcessor.ProcessAvatar(avatar);

            var avdesc = avatar.GetComponent<VRCAvatarDescriptor>();
            var animController = avdesc.baseAnimationLayers
                .First(l => l.animatorController != null)
                .animatorController as AnimatorController;

            Assert.AreNotEqual(originalAnimController, animController);
            Assert.False(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(animController)));

            AssertTempAsset(animController);
            var stateMachine = animController.layers[0].stateMachine;
            AssertTempAsset(stateMachine);
            var state = stateMachine.states[0].state;
            AssertTempAsset(state);

            // We might eventually optimize away rewriting clips that don't need changes, so just assert that they're
            // saved
            var blendTree = state.motion as BlendTree;
            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(blendTree)));

            var clip = blendTree.children[0].motion as AnimationClip;
            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clip)));

            // Validate that the menu is saved
            var menu = avdesc.expressionsMenu;
            AssertTempAsset(menu);
            AssertTempAsset(menu.controls[0].subMenu);
            var parameters = avdesc.expressionParameters;
            AssertTempAsset(parameters);
        }


        void AssertTempAsset(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            Assert.False(string.IsNullOrEmpty(path));

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.IsInstanceOf<GeneratedAssets>(mainAsset);
        }
    }
}