using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEngine;
using BuildContext = nadena.dev.ndmf.BuildContext;

namespace modular_avatar_tests
{
    public class ModularAvatarPlatformFilterTests : TestBase
    {
        private class DummyPlatformProvider : INDMFPlatformProvider
        {
            public string QualifiedName { get; set; }
            public string DisplayName => QualifiedName;
        }

        private void RunPlatformFilterPass(GameObject root, string platformName)
        {
            var context = new BuildContext(root, null, new DummyPlatformProvider { QualifiedName = platformName });
            new PlatformFilterPass().Process(context);
        }

        [Test]
        public void DeletesObject_WhenExcludedForPlatform()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter = child.AddComponent<ModularAvatarPlatformFilter>();
            filter.Platform = "TestPlatform";
            filter.ExcludePlatform = true;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNull(root.transform.Find("child"));
        }

        [Test]
        public void KeepsObject_WhenIncludedForPlatform()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter = child.AddComponent<ModularAvatarPlatformFilter>();
            filter.Platform = "TestPlatform";
            filter.ExcludePlatform = false;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNotNull(root.transform.Find("child"));
        }

        [Test]
        public void DeletesObject_WhenHasIncludesButNotIncluded()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter = child.AddComponent<ModularAvatarPlatformFilter>();
            filter.Platform = "OtherPlatform";
            filter.ExcludePlatform = false;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNull(root.transform.Find("child"));
        }

        [Test]
        public void KeepsObject_WhenNoFiltersMatchPlatform()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter = child.AddComponent<ModularAvatarPlatformFilter>();
            filter.Platform = "OtherPlatform";
            filter.ExcludePlatform = true;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNotNull(root.transform.Find("child"));
        }

        [Test]
        public void KeepsObject_WhenAnyIncludeMatchesPlatform_MultipleFilters()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter1 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter1.Platform = "TestPlatform";
            filter1.ExcludePlatform = false;
            var filter2 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter2.Platform = "OtherPlatform";
            filter2.ExcludePlatform = false;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNotNull(root.transform.Find("child"));
        }

        [Test]
        public void DeletesObject_WhenNoIncludeMatchesPlatform_MultipleFilters()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter1 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter1.Platform = "OtherPlatform1";
            filter1.ExcludePlatform = false;
            var filter2 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter2.Platform = "OtherPlatform2";
            filter2.ExcludePlatform = false;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNull(root.transform.Find("child"));
        }

        [Test]
        public void DeletesObject_WhenAnyExcludeMatchesPlatform_MultipleFilters()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter1 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter1.Platform = "TestPlatform";
            filter1.ExcludePlatform = true;
            var filter2 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter2.Platform = "OtherPlatform";
            filter2.ExcludePlatform = true;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNull(root.transform.Find("child"));
        }

        [Test]
        public void KeepsObject_WhenNoExcludeMatchesPlatform_MultipleFilters()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter1 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter1.Platform = "OtherPlatform1";
            filter1.ExcludePlatform = true;
            var filter2 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter2.Platform = "OtherPlatform2";
            filter2.ExcludePlatform = true;

            RunPlatformFilterPass(root, "TestPlatform");

            Assert.IsNotNull(root.transform.Find("child"));
        }

        [Test]
        public void ReportsError_WhenMixedIncludeExcludeOnSameObject()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var filter1 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter1.Platform = "TestPlatform";
            filter1.ExcludePlatform = false;
            var filter2 = child.AddComponent<ModularAvatarPlatformFilter>();
            filter2.Platform = "OtherPlatform";
            filter2.ExcludePlatform = true;

            var errors = ErrorReport.CaptureErrors(() => RunPlatformFilterPass(root, "TestPlatform"));
            Assert.IsTrue(errors.Any(e => e.TheError != null));
        }
    }
}
