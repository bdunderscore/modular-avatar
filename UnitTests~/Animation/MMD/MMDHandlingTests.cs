using System.Collections.Generic;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine.Assertions.Must;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace modular_avatar_tests.MMD
{
    public class MMDHandlingTests : TestBase
    {
        [Test]
        public void MMDMode_NoopHandling()
        {
            var prefab = CreatePrefab("MMDMode_Noop.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            Assert.AreEqual(3, fxc.layers.Length);
            Assert.AreEqual("L0", fxc.layers[0].name);
            Assert.AreEqual("L1", fxc.layers[1].name);
            Assert.AreEqual("L2", fxc.layers[2].name);
        }

        [Test]
        public void MMDMode_ReactiveComponent()
        {
            var prefab = CreatePrefab("MMDMode_Reactive.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;

            // RC, MMD, dummy, L0, L1, L2
            AssertMMDModeHandling(fxc, 4, 5);
            
            Assert.AreEqual(MergeBlendTreePass.BlendTreeLayerName, fxc.layers[0].name);
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxc.layers[1].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxc.layers[2].name);
            Assert.AreEqual("L0", fxc.layers[3].name);
            Assert.AreEqual("L1", fxc.layers[4].name);
            Assert.AreEqual("L2", fxc.layers[5].name);
        }

        [Test]
        public void MMDMode_MergeBefore()
        {
            var prefab = CreatePrefab("MMDMode_MergeBefore.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);            
            
            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            // M0, MMD, dummy, M1, M2, L0, L1, L2
            AssertMMDModeHandling(fxc, 6, 7);
            
            Assert.AreEqual(8, fxc.layers.Length);
            Assert.AreEqual("M0", fxc.layers[0].name);
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxc.layers[1].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxc.layers[2].name);
            Assert.AreEqual("M1", fxc.layers[3].name);
            Assert.AreEqual("M2", fxc.layers[4].name);
            Assert.AreEqual("L0", fxc.layers[5].name);
            Assert.AreEqual("L1", fxc.layers[6].name);
            Assert.AreEqual("L2", fxc.layers[7].name);
        }

        [Test]
        public void MMDMode_ManualOverride()
        {
            var prefab = CreatePrefab("MMDMode_Overrides.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            // Base, MMD, dummy, ForceOff, DefaultOn, DefaultOff, ForceOn
            AssertMMDModeHandling(fxc, 4, 6);
        }
        

        private void AssertMMDModeHandling(AnimatorController fxc, params int[] layers)
        {
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxc.layers[1].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxc.layers[2].name);

            var expectedLayers = new HashSet<int>(layers);

            foreach (var state in fxc.layers[1].stateMachine.states)
            {
                var actualLayers = new HashSet<int>();
                float expectedWeight = -1f;

                var behaviors = state.state.behaviours;

                switch (state.state.name)
                {
                    case MMDRelayPass.StateNameInitial:
                        Assert.IsEmpty(behaviors);
                        Assert.AreEqual(fxc.layers[1].stateMachine.defaultState, state.state);
                        continue;
                    
                    case MMDRelayPass.StateNameNotMMD:
                        expectedWeight = 1f;
                        break;
                    
                    case MMDRelayPass.StateNameMMD:
                        expectedWeight = 0f;
                        break;
                    
                    default:
                        Assert.Fail($"Unexpected state {state.state.name}");
                        break;
                }

                foreach (var behavior in state.state.behaviours)
                {
                    if (behavior is VRCAnimatorLayerControl lc)
                    {
                        Assert.AreEqual(expectedWeight, lc.goalWeight);
                        Assert.AreEqual(VRC_AnimatorLayerControl.BlendableLayer.FX, lc.playable);
                        Assert.IsTrue(actualLayers.Add(lc.layer));
                    }
                }
                
                Assert.That(expectedLayers, Is.EquivalentTo(actualLayers));
            }
        }
    }
}