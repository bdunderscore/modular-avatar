#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AvatarProcessor = nadena.dev.ndmf.AvatarProcessor;
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
            ctx.ActivateExtensionContext<core.editor.BuildContext>();
            ctx.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            var errors = ErrorReport.CaptureErrors(() =>
            {
                MergeAnimatorProcessor.ProcessAvatar(av, ctx);
                ctx.DeactivateAllExtensionContexts();
            });
            
            Assert.IsEmpty(errors);
        }

        [Test]
        public void MergeAnimationOverrideController()
        {
            var av = CreateRoot("root");

            var merge = av.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = LoadAsset<AnimatorOverrideController>("AOC_Override.overrideController");

            var ctx = new BuildContext(av, null);
            ctx.ActivateExtensionContext<core.editor.BuildContext>();
            ctx.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            var errors = ErrorReport.CaptureErrors(() =>
            {
                MergeAnimatorProcessor.ProcessAvatar(av, ctx);
                ctx.DeactivateAllExtensionContexts();
            });
            
            ctx.DeactivateAllExtensionContexts();
            
            Assert.IsEmpty(errors);

            var state = FindStateInLayer(findFxLayer(av, "Target"), "Anim1");
            Assert.IsTrue(state.motion.name.StartsWith("Anim2"));
        }

        [Test]
        public void MergeAnimation_ForcesFirstLayerToWeightOne()
        {
            var av = CreateRoot("root");
            
            var controller = new AnimatorController();
            var stateMachine = new AnimatorStateMachine();
            stateMachine.name = "test machine";
            
            controller.AddLayer(new AnimatorControllerLayer()
            {
                name = "Base",
                stateMachine = stateMachine,
                defaultWeight = 0
            });
            
            var merge = av.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = controller;
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            
            
            var ctx = new BuildContext(av, null);
            ctx.ActivateExtensionContext<core.editor.BuildContext>();
            ctx.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            var errors = ErrorReport.CaptureErrors(() =>
            {
                MergeAnimatorProcessor.ProcessAvatar(av, ctx);
                ctx.DeactivateAllExtensionContexts();
            });
            
            ctx.DeactivateAllExtensionContexts();
            
            Assert.IsEmpty(errors);

            var layer = ((AnimatorController) FindFxController(av).animatorController).layers[^1];
            Assert.AreEqual(1, layer.defaultWeight);
        }
    }
}

#endif