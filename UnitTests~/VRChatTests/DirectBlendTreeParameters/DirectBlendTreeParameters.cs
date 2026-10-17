#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;

namespace nadena.dev.modular_avatar.test.Tests.DirectBlendTreeParameters
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

#endif