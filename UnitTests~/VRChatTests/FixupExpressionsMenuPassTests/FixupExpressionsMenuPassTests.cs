#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using VRC.SDK3.Avatars.Components;

namespace UnitTests.FixupExpressionsMenuPassTests
{
    public class FixupExpressionsMenuPassTests : TestBase
    {
        [Test]
        public void FixupExpressionsMenuWorksOnOddSizedImages()
        {
            var prefab = CreatePrefab("OddSizedTextureTest.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var targetMenu = prefab.GetComponent<VRCAvatarDescriptor>().expressionsMenu;
            var targetElem = targetMenu.controls.First(c => c.name == "testmenu");

            var tex = targetElem.icon;
            
            // Rounds up to a multiple of four
            Assert.AreEqual(124, tex.width);
            Assert.AreEqual(48, tex.height);
            
            // ... which is needed in order for texture compression to succeed
            Assert.AreEqual(FixupExpressionsMenuPass.TargetFormat, tex.format);
        }
    }
}

#endif