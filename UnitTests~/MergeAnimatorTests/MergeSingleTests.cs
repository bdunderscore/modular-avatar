using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using BuildContext = nadena.dev.ndmf.BuildContext;

namespace UnitTests.MergeAnimatorTests
{
    internal class MergeSingleTests : TestBase
    {
        [Test]
        public void NoErrorWhenAnimatorIsNull()
        {
            var av = CreateRoot("root");

            var merge = av.AddComponent<ModularAvatarMergeAnimator>();

            var ctx = new BuildContext(av, null);
            ctx.ActivateExtensionContext<ModularAvatarContext>();
            ctx.ActivateExtensionContext<AnimationServicesContext>();

            var errors = ErrorReport.CaptureErrors(() => new MergeAnimatorProcessor().OnPreprocessAvatar(av, ctx));
            
            Assert.IsEmpty(errors);
        }

        [Test]
        public void MergeAnimationOverrideController()
        {
            var av = CreateRoot("root");

            var merge = av.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = LoadAsset<AnimatorOverrideController>("AOC_Override.overrideController");

            var ctx = new BuildContext(av, null);
            ctx.ActivateExtensionContext<ModularAvatarContext>();
            ctx.ActivateExtensionContext<AnimationServicesContext>();

            var errors = ErrorReport.CaptureErrors(() => new MergeAnimatorProcessor().OnPreprocessAvatar(av, ctx));
            
            Assert.IsEmpty(errors);

            var state = FindStateInLayer(findFxLayer(av, "Target"), "Anim1");
            Assert.IsTrue(state.motion.name.StartsWith("Anim2"));
        }
    }
}