#if MA_VRCSDK3_AVATARS

using System.Linq;
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
                "1", "2", "3", "4", "5"
            }, layerNames);
        }
    }
}

#endif