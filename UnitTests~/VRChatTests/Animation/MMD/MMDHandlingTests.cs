#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Assertions.Must;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

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
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxc.layers[2].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxc.layers[1].name);
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
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxc.layers[2].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxc.layers[1].name);
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
        
        [Test]
        public void MMDMode_OptInFirstLayer()
        {
            var prefab = CreatePrefab("MMDMode_OptInFirstLayer.prefab");

            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;

            var layerNames = fxc.layers.Select(l => l.name).ToList();
            Assert.That(layerNames, Is.EquivalentTo(new[]
            {
                MMDRelayPass.DummyLayerName,
                MMDRelayPass.ControlLayerName,
                "OptIn",
                "L0",
                "L1",
                "L2"
            }));
            AssertMMDModeLayerDrivers(fxc, 2, 4, 5);
        }
        
        [Test]
        public void MMDMode_WarnsAboutWriteDefaultsOff()
        {            
            var prefab = CreatePrefab("MMDMode_Overrides.prefab");
            
            // Before processing, manually create a layer with WD OFF states
            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            AddWdOffLayer(prefab);

            // Capture errors during processing
            var errors = ErrorReport.CaptureErrors(() =>
            {
                AvatarProcessor.ProcessAvatar(prefab);
            });
            
            // Check if warning was reported - look for the warning about WD OFF
            var hasWdOffWarning = errors.Any(error => error.TheError is SimpleError se &&
                                                      se.Severity == ErrorSeverity.NonFatal && 
                                                      se.TitleKey == "warning.mmd.wd_off");
            
            Assert.IsTrue(hasWdOffWarning, "Expected warning about WriteDefaults OFF in non-MMD layers was not reported");
        }

        private static void AddWdOffLayer(GameObject prefab)
        {
            // Add a layer with WD OFF states
            var ac = new AnimatorController();
            var testLayer = new AnimatorControllerLayer
            {
                name = "TestWDOffLayer",
                stateMachine = new AnimatorStateMachine()
            };
            testLayer.stateMachine.name = "TestWDOffLayer";

            var state = new AnimatorState()
            {
                name = "WDOffState",
                writeDefaultValues = false
            };
            testLayer.stateMachine.defaultState = state;
            testLayer.stateMachine.states = new[] { new ChildAnimatorState() { state = state } };

            ac.layers = new[]
            {
                testLayer
            };

            var mama = prefab.AddComponent<ModularAvatarMergeAnimator>();
            mama.animator = ac;
        }

        [Test]
        public void MMDMode_NoWarningWithoutMMDControl()
        {
            // Test that we don't warn about WD OFF states when no MMD Layer Control is present
            var prefab = CreatePrefab("MMDMode_Noop.prefab"); // This prefab has no MMD components
            
            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            AddWdOffLayer(prefab);
            
            // Capture errors during processing
            var errors = ErrorReport.CaptureErrors(() =>
            {
                AvatarProcessor.ProcessAvatar(prefab);
            });
            
            // Check that NO warning was reported about WD OFF
            var hasWdOffWarning = errors.Any(error => error.TheError is SimpleError se &&
                se.Severity == ErrorSeverity.NonFatal && 
                se.TitleKey == "warning.mmd.wd_off");
            
            Assert.IsFalse(hasWdOffWarning, "Unexpected warning about WriteDefaults OFF was reported when no MMD Layer Control is present");
        }
        

        private void AssertMMDModeHandling(AnimatorController fxc, params int[] layers)
        {
            Assert.AreEqual(MMDRelayPass.ControlLayerName, fxc.layers[2].name);
            Assert.AreEqual(MMDRelayPass.DummyLayerName, fxc.layers[1].name);

            AssertMMDModeLayerDrivers(fxc, layers);
        }

        private static void AssertMMDModeLayerDrivers(AnimatorController fxc, params int[] layers)
        {
            var expectedLayers = new HashSet<int>(layers);

            var controlLayer = fxc.layers.First(l => l.name == MMDRelayPass.ControlLayerName);
            foreach (var state in controlLayer.stateMachine.states)
            {
                var actualLayers = new HashSet<int>();
                float expectedWeight = -1f;

                var behaviors = state.state.behaviours;

                switch (state.state.name)
                {
                    case MMDRelayPass.StateNameInitial:
                        Assert.IsEmpty(behaviors);
                        Assert.AreEqual(state.state, controlLayer.stateMachine.defaultState);
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

#endif