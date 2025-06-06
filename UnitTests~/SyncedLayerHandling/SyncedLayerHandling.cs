﻿#if MA_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.SyncedLayerHandling
{
    public class SyncedLayerHandling : TestBase
    {
        [Test]
        public void WhenSyncedLayerIsOnBaseController_SyncedConfigIsPreserved()
        {
            var prefab = CreatePrefab("BaseController.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var mainLayer = findFxLayer(prefab, "main");
            var syncLayer = findFxLayer(prefab, "sync");

            Assert.AreEqual(FindFxLayerIndex(prefab, mainLayer), syncLayer.syncedLayerIndex);

            var m1State = FindStateInLayer(mainLayer, "m1");
            var m2State = FindStateInLayer(mainLayer, "m2");

            var m1Override = syncLayer.GetOverrideMotion(m1State);
            var m2Override = syncLayer.GetOverrideMotion(m2State);

            Assert.True(m1State.motion.name.Contains("m1"));
            Assert.True(m2State.motion.name.Contains("m2"));
            Assert.True(m1Override.name.Contains("m3"));
            Assert.True(m2Override.name.Contains("m4"));
        }

        [Test]
        public void WhenSyncedLayerIsOnMergedController_SyncedConfigIsPreserved()
        {
            var prefab = CreatePrefab("MergedController.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var mainLayer = findFxLayer(prefab, "main");
            var syncLayer = findFxLayer(prefab, "sync");

            Assert.AreEqual(FindFxLayerIndex(prefab, mainLayer), syncLayer.syncedLayerIndex);

            var m1State = FindStateInLayer(mainLayer, "m1");
            var m2State = FindStateInLayer(mainLayer, "m2");

            var m1Override = syncLayer.GetOverrideMotion(m1State);
            var m2Override = syncLayer.GetOverrideMotion(m2State);

            Assert.True(m1State.motion.name.Contains("m1"));
            Assert.True(m2State.motion.name.Contains("m2"));
            Assert.True(m1Override.name.Contains("m3"));
            Assert.True(m2Override.name.Contains("m4"));
        }

        [Test]
        public void WhenSyncedLayerIsOnBaseController_LayerControlBehaviorsArePreserved()
        {
            var prefab = CreatePrefab("BaseController.prefab");
            AvatarProcessor.ProcessAvatar(prefab);
            
            var mainLayer = findFxLayer(prefab, "main");
            var syncLayer = findFxLayer(prefab, "sync");

            Assert.AreEqual(FindFxLayerIndex(prefab, mainLayer), syncLayer.syncedLayerIndex);

            var m1State = FindStateInLayer(mainLayer, "m1");
            var overrides = syncLayer.GetOverrideBehaviours(m1State);
            Assert.AreEqual(1, overrides.Length);

            var layercontrol = overrides[0] as VRCAnimatorLayerControl;
            Assert.NotNull(layercontrol);

            Assert.AreEqual(1, layercontrol.layer);
        }

        [Test]
        public void WhenSyncedLayerIsOnMergedController_LayerControlBehaviorsAreNotAdjusted_CrossPlayable()
        {
            var prefab = CreatePrefab("MergedController.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var mainLayer = findFxLayer(prefab, "main");
            var syncLayer = findFxLayer(prefab, "sync");

            Assert.AreEqual(FindFxLayerIndex(prefab, mainLayer), syncLayer.syncedLayerIndex);

            var m1State = FindStateInLayer(mainLayer, "m1");
            var overrides = syncLayer.GetOverrideBehaviours(m1State);
            Assert.AreEqual(1, overrides.Length);

            var layercontrol = overrides[0] as VRCAnimatorLayerControl;
            Assert.NotNull(layercontrol);

            Assert.AreEqual(1, layercontrol.layer);
        }
        
        [Test]
        public void WhenSyncedLayerIsOnMergedController_LayerControlBehaviorsAreAdjusted_SamePlayable()
        {
            var prefab = CreatePrefab("MergedController_AC2.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = (AnimatorController) FindFxController(prefab).animatorController;
            var mainLayer = findFxLayer(prefab, "main");
            var syncLayer = findFxLayer(prefab, "sync");

            Assert.AreEqual(FindFxLayerIndex(prefab, mainLayer), syncLayer.syncedLayerIndex);

            var m1State = FindStateInLayer(mainLayer, "m1");
            var overrides = syncLayer.GetOverrideBehaviours(m1State);
            Assert.AreEqual(1, overrides.Length);

            var layercontrol = overrides[0] as VRCAnimatorLayerControl;
            Assert.NotNull(layercontrol);

            Assert.AreEqual(layercontrol.layer, fx.layers.TakeWhile(l => l.name != "main").Count());
        }
    }
}

#endif