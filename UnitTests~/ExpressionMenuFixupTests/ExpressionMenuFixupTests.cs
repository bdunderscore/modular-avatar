using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class ExpressionMenuFixupTests : TestBase
    {
        [Test]
        public void testExpMenuFixup()
        {
            var avatar = CreatePrefab("ExpressionsMenuFixupTestAvatar.prefab");
            AvatarProcessor.ProcessAvatar(avatar);

            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            var expMenu = descriptor.expressionsMenu;
            
            Assert.AreEqual(3, expMenu.controls.Count);
            
            Assert.AreEqual("testparam", expMenu.controls[0].parameter.name);
            Assert.AreEqual(256, expMenu.controls[0].icon.height);
            Assert.AreEqual(192, expMenu.controls[0].icon.width);
            
            Assert.AreEqual("", expMenu.controls[1].parameter.name);
            Assert.AreEqual(192, expMenu.controls[1].icon.height);
            Assert.AreEqual(256, expMenu.controls[1].icon.width);
            
            Assert.AreEqual("testparam", expMenu.controls[2].subParameters[0].name);
            Assert.AreEqual("", expMenu.controls[2].subParameters[1].name);

            foreach (var label in expMenu.controls[2].labels)
            {
                Assert.LessOrEqual(label.icon.width, 256);
                Assert.LessOrEqual(label.icon.height, 256);
            }
        }
    }
}