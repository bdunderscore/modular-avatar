using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;

namespace modular_avatar_tests
{
    public class LocalizationTest
    {
        [SetUp]
        public void Setup()
        {
            Localization.OverrideLanguage = null;
            Localization.Reload();
        }

        [TearDown]
        public void Teardown()
        {
            Localization.OverrideLanguage = null;
        }

        [Test]
        public void TestLanguageFallback()
        {
            Localization.OverrideLanguage = "test";
            Assert.AreEqual(Localization.S("test0.test_a"), "replaced");
            Assert.AreEqual(Localization.S("test0.test_b"), "test_b");
            Assert.AreEqual(Localization.S("test0.test_c"), "test0.test_c");

            Localization.OverrideLanguage = "en";
            Assert.AreEqual(Localization.S("test0.test_a"), "test_a");
            Assert.AreEqual(Localization.S("test0.test_b"), "test_b");
            Assert.AreEqual(Localization.S("test0.test_c"), "test0.test_c");
        }
    }
}