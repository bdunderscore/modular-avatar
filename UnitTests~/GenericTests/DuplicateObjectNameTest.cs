using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;

namespace nadena.dev.modular_avatar.test.EditModeTests
{
    public class DuplicateObjectNameTest : TestBase
    {
        /* TODO - move to build framework
        [Test]
        public void test_duplicate_object_names()
        {
            var avatar = CreateRoot("root");
            var c1 = CreateChild(avatar, "child");
            var c2 = CreateChild(avatar, "child");
    
            PathMappings.Init(avatar);
            c2.gameObject.name = "child2";
            Assert.AreEqual(PathMappings.MapPath("child"), "child");
        }
        */
    }
}