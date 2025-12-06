using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

public class SetupOutfitRenameTest : TestBase
{
    [Test]
    public void TestSetupHumanoidOutfit()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);
        
        var root_chest = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);

        var outfit = CreateCommonPrefab("shapell.fbx");
        outfit.transform.SetParent(root.transform);
        var outfit_chest = outfit.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);
        outfit_chest.name = "c";

        SetupOutfit.SetupOutfitUI(outfit);

        Assert.AreEqual(root_chest.name, outfit_chest.name);
    }

    [Test]
    public void TestSetupUpperChestOutfit()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);
        
        var root_armature = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).parent.gameObject;
        var root_chest = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest).gameObject;

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "armature");
        var outfit_hips = CreateChild(outfit_armature, "hips");
        var outfit_spine = CreateChild(outfit_hips, "spine");
        var outfit_chest = CreateChild(outfit_spine, "chest");
        var outfit_upperchest = CreateChild(outfit_chest, "upperchest");

        SetupOutfit.SetupOutfitUI(outfit);

        Assert.AreSame(root_armature, outfit_armature.GetComponent<ModularAvatarMergeArmature>().mergeTargetObject);
        Assert.AreSame(root_chest, outfit_upperchest.GetComponent<ModularAvatarMergeArmature>().mergeTargetObject);
    }

    [Test]
    public void TestSetupSetupedOutfit()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);
        
        var root_hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        var root_armature = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).parent.gameObject;

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "armature");
        var outfit_hips = CreateChild(outfit_armature, "HIP");
        outfit_armature.AddComponent<ModularAvatarMergeArmature>();

        SetupOutfit.SetupOutfitUI(outfit);

        Assert.AreEqual(root_armature, outfit_armature.GetComponent<ModularAvatarMergeArmature>().mergeTargetObject);
        Assert.AreEqual(root_hips.name, outfit_hips.name);
    }
}
