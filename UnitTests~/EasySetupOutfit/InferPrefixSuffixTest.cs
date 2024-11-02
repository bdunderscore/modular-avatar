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
        var outfit_hips = CreateChild(outfit_armature, "hips");

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
        var outfit_hips = CreateChild(outfit_armature, "pre_Hips.suf");

        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_hips.parent.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;

        outfit_mama.InferPrefixSuffix();

        Assert.AreEqual("pre_", outfit_mama.prefix);
        Assert.AreEqual(".suf", outfit_mama.suffix);
    }

    [Test]
    public void TestSameHipsName_Success()
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

        var outfit_mama = outfit_armature.AddComponent<ModularAvatarMergeArmature>();
        outfit_mama.mergeTarget = new AvatarObjectReference();
        outfit_mama.mergeTarget.referencePath = RuntimeUtil.RelativePath(root, root_hips.parent.gameObject);
        outfit_mama.LockMode = ArmatureLockMode.BaseToMerge;

        outfit_mama.InferPrefixSuffix();

        Assert.AreEqual("pre_", outfit_mama.prefix);
        Assert.AreEqual("2.suf", outfit_mama.suffix);
    }

    [Test]
    public void TestSameHipsName_Fail()
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

        // Current(v1.10.x) InferPrefixSuffix fail to infer prefix/suffix when avatar has unique prefix/suffix and outfit has their name
        Assert.AreNotEqual("pre_", outfit_mama.prefix);
        Assert.AreNotEqual(".suf", outfit_mama.suffix);
    }
}
