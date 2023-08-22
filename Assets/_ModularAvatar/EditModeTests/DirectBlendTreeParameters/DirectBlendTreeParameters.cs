using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;

namespace modular_avatar_tests.DirectBlendTreeParameters
{
    public class DirectBlendTreeParameters : TestBase
    {
        [Test]
        public void RemapsDirectBlendTreeParameters()
        {
            var prefab = CreatePrefab("DirectBlendTreeParameters.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var layerName = "merged";

            var motion = findFxMotion(prefab, layerName);
            var blendTree = motion as BlendTree;
            Assert.NotNull(blendTree);

            var children = blendTree.children;
            Assert.AreEqual(children[0].directBlendParameter, "A"); //not remapped
            Assert.AreEqual(children[1].directBlendParameter, "C"); //remapped
        }
    }
}