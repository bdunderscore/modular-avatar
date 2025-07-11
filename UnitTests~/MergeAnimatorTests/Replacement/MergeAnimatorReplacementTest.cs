﻿#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

namespace UnitTests.MergeAnimatorTests.Replacement
{
    public class MergeAnimatorReplacementTest : TestBase
    {
        [Test]
        public void MergeWithReplacement()
        {
            var prefab = CreatePrefab("happycase.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;

            var layers = fxc.layers.Where(l => !MMDRelayPass.IsRelayLayer(l.name)).ToList();
            
            Assert.AreEqual(2, layers.Count);
            Assert.AreEqual("2", layers[0].name);
            Assert.AreEqual("3", layers[1].name);
        }

        [Test]
        public void ReplaceRespectsPriorities()
        {
            var prefab = CreatePrefab("happycase.prefab");
            var merge = prefab.transform.Find("merge_replace").GetComponent<ModularAvatarMergeAnimator>();
            merge.layerPriority = 1;
            
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            var layers = fxc.layers.Where(l => !MMDRelayPass.IsRelayLayer(l.name)).ToList();
            
            Assert.AreEqual(2, layers.Count);
            Assert.AreEqual("3", layers[0].name);
            Assert.AreEqual("2", layers[1].name);
        }

        [Test]
        public void MultipleReplacementIsForbidden()
        {
            var prefab = CreatePrefab("doublereplace.prefab");
            
            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(prefab));

            Assert.AreEqual(1, errors.Count());
            var err = (SimpleError)errors.First().TheError;
            Assert.AreEqual("error.merge_animator.multiple_replacements", err.TitleKey);
            Assert.AreEqual(2, err._references.Count());
        }

        [Test]
        public void ReplacementPreservesMergedBlendTrees()
        {
            var prefab = CreatePrefab("ReplacePreservesMergedBlendTrees.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            
            Assert.IsTrue(fxc.layers.Any(l => l.name == MergeBlendTreePass.BlendTreeLayerName));
        }
    }
}

#endif