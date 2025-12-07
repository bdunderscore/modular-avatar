using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEngine;

public class InferPrefixSuffixTest : TestBase
{
    [Test]
    public void TestNoPrefixSuffix()
    {
        var root = CreateCommonPrefab("shapell.fbx");
#if MA_VRCSDK3_AVATARS
        root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        var root_hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        root_hips.name = "hip";

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "armature");
        var outfit_hips = CreateChild(outfit_armature, "hip");

        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_hips.parent.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;

        outfit_mama.InferPrefixSuffix();

        Assert.AreEqual("", outfit_mama.prefix);
        Assert.AreEqual("", outfit_mama.suffix);
    }

    [Test]
    public void TestDifferentHipsName()
    {
        var root = CreateCommonPrefab("shapell.fbx");
#if MA_VRCSDK3_AVATARS
        root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        var root_hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        root_hips.name = "hip";

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "armature");
        var outfit_hips = CreateChild(outfit_armature, "pre_hips.suf");


        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_hips.parent.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;

        outfit_mama.InferPrefixSuffix();
        
        // Initially, we determine "hip" to be the match
        Assert.AreEqual("pre_", outfit_mama.prefix);
        Assert.AreEqual("s.suf", outfit_mama.suffix);
        
        // Now, add the legs.
        var outfit_left_leg = CreateChild(outfit_hips, "pre_upleg.l.suf");
        var outfit_right_leg = CreateChild(outfit_hips, "pre_upleg.r.suf");
        
        // Now, we match 3 with ".suf" vs 1 with "s.suf", so the inference should change.
        outfit_mama.InferPrefixSuffix();

        Assert.AreEqual("pre_", outfit_mama.prefix);
        Assert.AreEqual(".suf", outfit_mama.suffix);
    }

    [Test]
    public void TestSameHipsName_Multiple()
    {
        var root = CreateCommonPrefab("shapell.fbx");
#if MA_VRCSDK3_AVATARS
        root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        var root_hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        root_hips.name = "TEST_HI";

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "armature");
        var outfit_hips = CreateChild(outfit_armature, "pre_TEST_HI2.suf"); // Make it a little bit different name to confirm it matches the current implementation
        var outfit_spine = CreateChild(outfit_hips, "pre_Spine2.suf");
        var outfit_chest = CreateChild(outfit_spine, "pre_Bust2.suf");

        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_hips.parent.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;

        outfit_mama.InferPrefixSuffix();

        Assert.AreEqual("pre_", outfit_mama.prefix);
        Assert.AreEqual("2.suf", outfit_mama.suffix);
    }

    [Test]
    public void TestSameHipsName_Single()
    {
        var root = CreateCommonPrefab("shapell.fbx");
#if MA_VRCSDK3_AVATARS
        root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        var root_hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        root_hips.name = "TE_HIPS_ST";

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "armature");
        var outfit_hips = CreateChild(outfit_armature, "pre_TE_HIPS_ST.suf");

        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_hips.parent.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;

        outfit_mama.InferPrefixSuffix();

        Assert.AreEqual("pre_", outfit_mama.prefix);
        Assert.AreEqual(".suf", outfit_mama.suffix);
    }

    [Test]
    public void TestSpuriousMatch()
    {
        var root = CreateCommonPrefab("shapell.fbx");
#if MA_VRCSDK3_AVATARS
        root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        var animator = root.GetComponent<Animator>();
        var root_hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var root_armature = root_hips.parent;

        root_hips.gameObject.name = "bone_pelvis";
        root_armature.gameObject.name = "bone_root";
        animator.GetBoneTransform(HumanBodyBones.Spine).gameObject.name = "bone_Spine";
            
        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "bone_root");
        var outfit_hips = CreateChild(outfit_armature, "bone_pelvis");
        var outfit_spine = CreateChild(outfit_hips, "bone_Spine");
        
        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_armature.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;
        
        outfit_mama.InferPrefixSuffix();
        
        Assert.AreEqual("", outfit_mama.prefix);
        Assert.AreEqual("", outfit_mama.suffix);
    }
}
