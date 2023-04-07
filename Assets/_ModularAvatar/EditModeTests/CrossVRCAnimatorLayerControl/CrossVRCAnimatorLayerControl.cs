using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.CrossVRCAnimatorLayerControl
{
    public class CrossVRCAnimatorLayerControl : TestBase
    {
        private static AnimatorControllerLayer FindLayer(GameObject prefab, VRCAvatarDescriptor.AnimLayerType layerType, string layerName)
        {
            var animLayer = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .FirstOrDefault(l => l.type == layerType);

            Assert.NotNull(animLayer);
            var ac = animLayer.animatorController as AnimatorController;
            Assert.NotNull(ac);
            Assert.False(animLayer.isDefault);

            var layer = ac.layers.FirstOrDefault(l => l.name == layerName);
            Assert.NotNull(layer);
            return layer;
        }

        [Test]
        public void CrossVRCAnimatorLayerControl_CheckNotAdjust()
        {
            var prefab = CreatePrefab("CrossVRCAnimatorLayerControl_notAdjust.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var baseLayer = FindLayer(prefab, VRCAvatarDescriptor.AnimLayerType.Base, "Adjuster"); ;

            var s1State = FindStateInLayer(baseLayer, "s1");
            var s2State = FindStateInLayer(baseLayer, "s2");
            var s3State = FindStateInLayer(baseLayer, "s3");
            var s4State = FindStateInLayer(baseLayer, "s4");

            Assert.AreEqual(1, s1State.behaviours.Length);
            Assert.AreEqual(1, s2State.behaviours.Length);
            Assert.AreEqual(1, s3State.behaviours.Length);
            Assert.AreEqual(1, s4State.behaviours.Length);

            var lc1 = s1State.behaviours[0] as VRCAnimatorLayerControl;
            var lc2 = s2State.behaviours[0] as VRCAnimatorLayerControl;
            var lc3 = s3State.behaviours[0] as VRCAnimatorLayerControl;
            var lc4 = s4State.behaviours[0] as VRCAnimatorLayerControl;

            Assert.NotNull(lc1);
            Assert.NotNull(lc2);
            Assert.NotNull(lc3);
            Assert.NotNull(lc4);

            Assert.AreEqual(1, lc1.layer);
            Assert.AreEqual(1, lc2.layer);
            Assert.AreEqual(1, lc3.layer);
            Assert.AreEqual(1, lc4.layer);
        }

        [Test]
        public void CrossVRCAnimatorLayerControl_CheckAdjust()
        {
            var prefab = CreatePrefab("CrossVRCAnimatorLayerControl.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var baseLayer = FindLayer(prefab, VRCAvatarDescriptor.AnimLayerType.Base, "Adjuster");;

            var s1State = FindStateInLayer(baseLayer, "s1");
            var s2State = FindStateInLayer(baseLayer, "s2");
            var s3State = FindStateInLayer(baseLayer, "s3");
            var s4State = FindStateInLayer(baseLayer, "s4");

            Assert.AreEqual(1, s1State.behaviours.Length);
            Assert.AreEqual(1, s2State.behaviours.Length);
            Assert.AreEqual(1, s3State.behaviours.Length);
            Assert.AreEqual(1, s4State.behaviours.Length);

            var lc1 = s1State.behaviours[0] as VRCAnimatorLayerControl;
            var lc2 = s2State.behaviours[0] as VRCAnimatorLayerControl;
            var lc3 = s3State.behaviours[0] as VRCAnimatorLayerControl;
            var lc4 = s4State.behaviours[0] as VRCAnimatorLayerControl;

            Assert.NotNull(lc1);
            Assert.NotNull(lc2);
            Assert.NotNull(lc3);
            Assert.NotNull(lc4);

            Assert.AreEqual(2, lc1.layer);
            Assert.AreEqual(2, lc2.layer);
            Assert.AreEqual(2, lc3.layer);
            Assert.AreEqual(2, lc4.layer);
        }
    }
}


