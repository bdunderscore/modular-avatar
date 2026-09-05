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

            var toggle = root.AddComponent<ModularAvatarObjectToggle>();
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

        [Test]
        public void GenericBuild_AppliesDefaultActiveMenuToggleWithoutAnimatorLogic()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);

            var target = CreateChild(root, "target");
            target.SetActive(false);
            var controller = CreateChild(root, "controller");
            var menuItem = controller.AddComponent<ModularAvatarMenuItem>();
            menuItem.PortableControl.Type = PortableControlType.Toggle;
            menuItem.PortableControl.Parameter = "toggle";
            menuItem.PortableControl.Value = 1;
            menuItem.isDefault = true;
            var toggle = controller.AddComponent<ModularAvatarObjectToggle>();
            toggle.Objects = new()
            {
                new ToggledObject
                {
                    Object = new AvatarObjectReference(target),
                    Active = true
                }
            };

            var context = CreateContext(root, WellKnownPlatforms.Generic);
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.ActivateExtensionContextRecursive<ReadablePropertyExtension>();

            new ReactiveObjectPassV2(context).Execute();

            Assert.That(target.activeSelf, Is.True);
            Assert.That(root.GetComponent<Animator>().runtimeAnimatorController, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GenericBuild_PropagatesAlwaysOnObjectDriverToDependentRule(bool driverActive)
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);

            var driverTarget = CreateChild(root, "driver target");
            driverTarget.SetActive(!driverActive);
            var dependentTarget = CreateChild(root, "dependent target");
            dependentTarget.SetActive(true);

            var driver = root.AddComponent<ModularAvatarObjectToggle>();
            driver.Objects = new()
            {
                new ToggledObject
                {
                    Object = new AvatarObjectReference(driverTarget),
                    Active = driverActive
                }
            };

            var dependent = driverTarget.AddComponent<ModularAvatarObjectToggle>();
            dependent.Objects = new()
            {
                new ToggledObject
                {
                    Object = new AvatarObjectReference(dependentTarget),
                    Active = false
                }
            };

            var context = CreateContext(root, WellKnownPlatforms.Generic);
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.ActivateExtensionContextRecursive<ReadablePropertyExtension>();

            new ReactiveObjectPassV2(context).Execute();

            Assert.That(dependentTarget.activeSelf, Is.EqualTo(!driverActive));
            Assert.That(root.GetComponent<Animator>().runtimeAnimatorController, Is.Null);
        }
    }
}
