using modular_avatar_tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace UnitTests.MergeAnimatorTests.SyncedLayerOverrideInSubStatemachine
{
    public class SyncedLayerOverrideInSubStateMachine : TestBase
    {
        [Test]
        public void SyncedLayerOverride_usesCorrectStateOverride()
        {
            var controller = LoadAsset<AnimatorController>("syncedlayer.controller");
            var root = CreateRoot("root");
            var vrc_descriptor = root.GetComponent<VRCAvatarDescriptor>();

            var layers = vrc_descriptor.baseAnimationLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    layer.animatorController = controller;
                    layer.isDefault = false;
                }

                layers[i] = layer;
            }

            AvatarProcessor.ProcessAvatar(root);

            var fx = FindFxController(root);
            
            var ac = (AnimatorController) fx.animatorController;
            var layer2 = ac.layers[2];
            var motion = layer2.GetOverrideMotion(ac.layers[1].stateMachine.stateMachines[0].stateMachine.states[0].state);
            Assert.NotNull(motion);
        }
    }
}