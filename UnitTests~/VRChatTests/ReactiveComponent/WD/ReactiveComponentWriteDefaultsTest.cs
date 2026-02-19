#if MA_VRCSDK3_AVATARS

using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

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

        var fx = findFxLayer(root, "MA Responsive: GameObject");
        Assert.AreEqual(
            controllerPath.StartsWith("rc_wd_on"),
            fx.stateMachine.defaultState.writeDefaultValues
        );
    }
}

#endif