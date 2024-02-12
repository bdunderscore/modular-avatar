#if MA_VRCSDK3_AVATARS

using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AvatarProcessor = nadena.dev.ndmf.AvatarProcessor;

namespace modular_avatar_tests
{
    public class MergeBlendTreeTest : TestBase
    {
        [Test]
        public void SimpleMergeTest()
        {
            BlendTree bt1 = new BlendTree();
            bt1.blendParameter = "abcd";
            bt1.blendParameterY = "defg";
            bt1.blendType = BlendTreeType.Simple1D;
            bt1.AddChild(AnimationTestUtil.AnimationWithPath("a"));
            
            BlendTree bt2 = new BlendTree();
            bt2.blendParameter = "p1";
            bt2.blendParameterY = "p2";
            bt2.blendType = BlendTreeType.FreeformCartesian2D;
            bt2.AddChild(AnimationTestUtil.AnimationWithPath("b"));

            var root = CreateRoot("root");
            var c1 = CreateChild(root, "child1");
            var mergeComponent = c1.AddComponent<ModularAvatarMergeBlendTree>();
            mergeComponent.BlendTree = bt1;
            mergeComponent.PathMode = MergeAnimatorPathMode.Relative;
            CreateChild(c1, "a");
            
            var c2 = CreateChild(root, "child2");
            var mergeComponent2 = c2.AddComponent<ModularAvatarMergeBlendTree>();
            mergeComponent2.BlendTree = bt2;
            mergeComponent2.PathMode = MergeAnimatorPathMode.Absolute;
            CreateChild(c2, "b");
            
            AvatarProcessor.ProcessAvatar(root);

            var fxController = FindFxController(root).animatorController as AnimatorController;
            var fx = findFxLayer(root, MergeBlendTreePass.BlendTreeLayerName);
            Assert.AreSame(fxController.layers[0].stateMachine, fx.stateMachine);
            Assert.AreEqual(1, fx.stateMachine.states.Length);

            var motion = fx.stateMachine.states[0].state.motion as BlendTree;
            Assert.AreEqual(BlendTreeType.Direct, motion.blendType);
            Assert.AreEqual(2, motion.children.Length);
            Assert.AreEqual(MergeBlendTreePass.ALWAYS_ONE, motion.children[0].directBlendParameter);
            Assert.AreEqual(MergeBlendTreePass.ALWAYS_ONE, motion.children[1].directBlendParameter);
            AnimationTestUtil.AssertAnimationHasPath(((BlendTree)motion.children[0].motion).children[0].motion as AnimationClip, "child1/a");
            AnimationTestUtil.AssertAnimationHasPath(((BlendTree)motion.children[1].motion).children[0].motion as AnimationClip, "b");
            
            Assert.IsTrue(fxController.parameters.Any(p => 
                p.name == "abcd" && p.type == AnimatorControllerParameterType.Float));
            Assert.IsFalse(fxController.parameters.Any(p =>
                p.name == "defg" && p.type == AnimatorControllerParameterType.Float));
            Assert.IsTrue(fxController.parameters.Any(p =>
                p.name == "p1" && p.type == AnimatorControllerParameterType.Float));
            Assert.IsTrue(fxController.parameters.Any(p =>
                p.name == "p2" && p.type == AnimatorControllerParameterType.Float));
            Assert.IsTrue(fxController.parameters.Any(p =>
                p.name == MergeBlendTreePass.ALWAYS_ONE && p.type == AnimatorControllerParameterType.Float
                && Math.Abs(p.defaultFloat - 1.0f) < 0.0001f));
        }

        [Test]
        public void AlternateRootTest()
        {
            BlendTree bt = new BlendTree();
            bt.AddChild(AnimationTestUtil.AnimationWithPath("a"));

            var root = CreateRoot("root");
            var c1 = CreateChild(root, "child1");
            var mergeComponent = c1.AddComponent<ModularAvatarMergeBlendTree>();
            mergeComponent.BlendTree = bt;
            mergeComponent.PathMode = MergeAnimatorPathMode.Relative;
            mergeComponent.RelativePathRoot.referencePath = "child2";
            CreateChild(c1, "a");
            
            var c2 = CreateChild(root, "child2");
            CreateChild(c2, "a");
            
            AvatarProcessor.ProcessAvatar(root);
            
            var fx = findFxLayer(root, MergeBlendTreePass.BlendTreeLayerName);
            var motion = fx.stateMachine.states[0].state.motion as BlendTree;

            AnimationTestUtil.AssertAnimationHasPath(((BlendTree)motion.children[0].motion).children[0].motion as AnimationClip, "child2/a");
        }

        [Test]
        public void MergeOrderTest()
        {
            var root = CreateRoot("root");
            var bt = new BlendTree();
            
            var c1 = CreateChild(root, "child1");
            var mergeComponent = c1.AddComponent<ModularAvatarMergeBlendTree>();
            mergeComponent.BlendTree = bt;

            TestMerge(root, "m1");
            TestMerge(root, "m2").layerPriority = int.MinValue;
            TestMerge(root, "m3").layerPriority = int.MaxValue;

            AvatarProcessor.ProcessAvatar(root);
            
            var layerNames = (FindFxController(root).animatorController as AnimatorController)
                .layers.Select(l => l.name).ToArray();
            
            Assert.AreEqual(new[] {MergeBlendTreePass.BlendTreeLayerName, "m2", "Eyes", "FaceMood", "m1", "m3"}, layerNames);
        }

        ModularAvatarMergeAnimator TestMerge(GameObject root, string mergeName, Motion motion = null)
        {
            var obj = CreateChild(root, mergeName);
            var merge = obj.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = AnimationTestUtil.TestController(mergeName, motion);

            return merge;
        }
    }
}

#endif