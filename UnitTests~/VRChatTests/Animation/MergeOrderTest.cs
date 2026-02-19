#if MA_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class MergeOrderTest : TestBase
    {
        [Test]
        public void TestMergeOrder()
        {
            var root = CreatePrefab("MergeOrderTest.prefab");
            
            AvatarProcessor.ProcessAvatar(root);
            
            var fxController = FindController(root, VRCAvatarDescriptor.AnimLayerType.FX);
                      
            var layerNames = (FindController(root, VRCAvatarDescriptor.AnimLayerType.FX).animatorController as AnimatorController)
                .layers.Select(l => l.name).ToArray();
            
            Assert.AreEqual(new []
            {
                MMDRelayPass.DummyLayerName, MMDRelayPass.DummyLayerName, MMDRelayPass.ControlLayerName, "Modular Avatar: Responsive Objects Blendtree"
            }, layerNames);
        }
    }
}

#endif