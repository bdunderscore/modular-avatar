#if MA_VRCSDK3_AVATARS

using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class LayerPruningTest : TestBase
    {
        [Test]
        public void Test()
        {
            var prefab = CreatePrefab("LayerPruningTest.prefab");
            AvatarProcessor.ProcessAvatar(prefab);
            
            var fxController = (AnimatorController) FindController(prefab, VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            var l0 = fxController.layers[0];
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxController.layers[2].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxController.layers[1].name);
            var l1 = fxController.layers[3];
            var l2 = fxController.layers[4];
            var l3 = fxController.layers[5];
            var l3a = fxController.layers[6];

            Assert.AreEqual("Base Layer", l0.name);
            Assert.AreEqual("L1", l1.name);
            Assert.AreEqual("L2", l2.name);
            Assert.AreEqual("L3", l3.name);
            Assert.AreEqual("L3.a", l3a.name);

            // The layer control behavior referencing the deleted layer should be removed
            Assert.AreEqual(3, l3.stateMachine.defaultState.behaviours.Length);
            Assert.AreEqual("2", ((VRCAnimatorLayerControl)l3.stateMachine.defaultState.behaviours[0]).debugString);
            Assert.IsTrue(l3.stateMachine.defaultState.behaviours[1] is VRCAnimatorTrackingControl);
            Assert.AreEqual("3", ((VRCAnimatorLayerControl)l3.stateMachine.defaultState.behaviours[2]).debugString);
            Assert.AreEqual(FindFxLayerIndex(prefab, l3), ((VRCAnimatorLayerControl)l3.stateMachine.defaultState.behaviours[0]).layer);
            
            Assert.AreEqual(1, l3a.stateMachine.defaultState.behaviours.Length);
            Assert.AreEqual(FindFxLayerIndex(prefab, l3), ((VRCAnimatorLayerControl)l3a.stateMachine.defaultState.behaviours[0]).layer);
        }
    }
}

#endif