using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.ReactiveComponent
{
    public class GenericPlatformReactiveObjectTests : TestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public void GenericBuild_AppliesAlwaysOnObjectToggleWithoutAnimatorLogic(bool active)
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);

            var target = CreateChild(root, "target");
            target.SetActive(!active);

            var controller = CreateChild(root, "controller");
            var menuItem = controller.AddComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Parameter = "ReactiveInput";
            menuItem.isDefault = true;
            var toggle = controller.AddComponent<ModularAvatarObjectToggle>();
            toggle.Objects = new()
            {
                new ToggledObject
                {
                    Object = new AvatarObjectReference(target),
                    Active = active
                }
            };

            var context = CreateContext(root, WellKnownPlatforms.Generic);
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.ActivateExtensionContextRecursive<ReadablePropertyExtension>();

            new ReactiveObjectPassV2(context).Execute();

            Assert.That(target.activeSelf, Is.EqualTo(active));
            Assert.That(root.GetComponent<Animator>().runtimeAnimatorController, Is.Null);
        }
    }
}
