using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;

namespace modular_avatar_tests
{
    public class MergeOrderTest : TestBase
    {
        [Test]
        public void TestMergeOrder()
        {
            var root = CreatePrefab("MergeOrderTest.prefab");
            
            AvatarProcessor.ProcessAvatar(root);
            
            var fxController = FindFxController(root);
                      
            var layerNames = (FindFxController(root).animatorController as AnimatorController)
                .layers.Select(l => l.name).ToArray();
            
            Assert.AreEqual(new []
            {
                "1", "2", "3", "4", "5"
            }, layerNames);
        }
    }
}