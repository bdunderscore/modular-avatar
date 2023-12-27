using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
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
            
            var fxController = (AnimatorController) FindFxController(prefab).animatorController;
            var l0 = fxController.layers[0];
            var l1 = fxController.layers[1];
            var l2 = fxController.layers[2];
            var l3 = fxController.layers[3];
            var l3a = fxController.layers[4];

            Assert.AreEqual("Base Layer", l0.name);
            Assert.AreEqual("L1", l1.name);
            Assert.AreEqual("L2", l2.name);
            Assert.AreEqual("L3", l3.name);
            Assert.AreEqual("L3.a", l3a.name);

            Assert.AreEqual(2, l3.stateMachine.defaultState.behaviours.Length);
            Assert.AreEqual(4, ((VRCAnimatorLayerControl)l3.stateMachine.defaultState.behaviours[0]).layer);
            
            Assert.AreEqual(1, l3a.stateMachine.defaultState.behaviours.Length);
            Assert.AreEqual(3, ((VRCAnimatorLayerControl)l3a.stateMachine.defaultState.behaviours[0]).layer);
        }
    }
}