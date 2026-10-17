#if MA_VRCSDK3_AVATARS

using System.Collections;
using System.Collections.Generic;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.test.Tests
{
    public class ReactiveComponentWriteDefaultsTest : TestBase
    {
        [Test]
        public void RCObjectWriteDefaults([Values("rc_wd_on.controller", "rc_wd_off.controller")] string controllerPath)
        {
            var root = CreatePrefab("rc_wd.prefab");
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            var controller = LoadAsset<AnimatorController>(controllerPath);
            
            var baseLayers = avDesc.baseAnimationLayers;
            for (int i = 0; i < baseLayers.Length; i++)
            {
                baseLayers[i].animatorController = controller;
                baseLayers[i].isDefault = false;
            }
            avDesc.baseAnimationLayers = baseLayers;
            avDesc.customizeAnimationLayers = true;
    
            AvatarProcessor.ProcessAvatar(root);
    
            var baseLayer = findFxLayer(root, "MA/RC Base");
            var fx = findFxLayer(root, "MA/RC Apply");
            // Blend trees must be set with WD on, regardless of what the rest of the avatar is doing.
            Assert.AreEqual(
                true,
                fx.stateMachine.defaultState.writeDefaultValues
            );
            Assert.AreEqual(
                true,
                baseLayer.stateMachine.defaultState.writeDefaultValues
            );
        }
    }
}

#endif