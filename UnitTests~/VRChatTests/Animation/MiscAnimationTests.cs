#if MA_VRCSDK3_AVATARS

using nadena.dev.ndmf;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace modular_avatar_tests
{
    public class MiscAnimationTests : TestBase
    {
        [Test]
        public void HighQualityCurvesSettingPreserved()
        {
            var prefab = CreatePrefab("HighQualityCurvesSettingPreserved.prefab");
            var context = new BuildContext(prefab, null);
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.DeactivateExtensionContext<AnimatorServicesContext>();
            context.DeactivateExtensionContext<VirtualControllerContext>();

            var layer = findFxLayer(prefab, "Base Layer");

            var hq_on = FindStateInLayer(layer, "HQ_ON");
            var hq_off = FindStateInLayer(layer, "HQ_OFF");

            Assert.True(new SerializedObject(hq_on.motion).FindProperty("m_UseHighQualityCurve").boolValue);
            Assert.False(new SerializedObject(hq_off.motion).FindProperty("m_UseHighQualityCurve").boolValue);
        }

        [Test]
        public void RelativePathTest()
        {
            var root = CreateRoot("root");
            
            var c1 = CreateChild(root, "child1");
            var c2 = CreateChild(root, "child2");
            var c3 = CreateChild(root, "child3");
            
            var a1 = CreateChild(c1, "a");
            var a2 = CreateChild(c2, "a");
            var a3 = CreateChild(c3, "a");
            var a0 = CreateChild(root, "a");

            var merge_rel_1 = AnimationTestUtil.TestController("T1", AnimationTestUtil.AnimationWithPath("a"));
            var merge_rel_2 = AnimationTestUtil.TestController("T2", AnimationTestUtil.AnimationWithPath("a"));
            var merge_abs = AnimationTestUtil.TestController("T3", AnimationTestUtil.AnimationWithPath("a"));
            
            var merge_rel_1_comp = c1.AddComponent<ModularAvatarMergeAnimator>();
            merge_rel_1_comp.animator = merge_rel_1;
            merge_rel_1_comp.pathMode = MergeAnimatorPathMode.Relative;
            merge_rel_1_comp.relativePathRoot.referencePath = "child2";
            
            var merge_rel_2_comp = c3.AddComponent<ModularAvatarMergeAnimator>();
            merge_rel_2_comp.animator = merge_rel_2;
            merge_rel_2_comp.pathMode = MergeAnimatorPathMode.Relative;
            
            var merge_abs_comp = c2.AddComponent<ModularAvatarMergeAnimator>();
            merge_abs_comp.animator = merge_abs;
            merge_abs_comp.pathMode = MergeAnimatorPathMode.Absolute;
            
            AvatarProcessor.ProcessAvatar(root);
            
            AnimationTestUtil.AssertAnimationHasPath(findFxMotion(root, "T3") as AnimationClip, "a");
            AnimationTestUtil.AssertAnimationHasPath(findFxMotion(root, "T1") as AnimationClip, "child2/a");
            AnimationTestUtil.AssertAnimationHasPath(findFxMotion(root, "T2") as AnimationClip, "child3/a");
        }
    }
}

#endif