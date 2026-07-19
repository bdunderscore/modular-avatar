using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
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
        var outfitRoot = outfit.GetComponent<ModularAvatarOutfitRoot>();
        Assert.NotNull(outfitRoot);
        Assert.AreSame(outfit.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).parent,
            outfitRoot.armatureRoot);
    }

    [Test]
    public void SetupOutfitCanBeRunTwiceWithoutAddingDuplicateComponents()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);

        var outfit = CreateCommonPrefab("shapell.fbx");
        outfit.transform.SetParent(root.transform);
        var outfitArmature = outfit.GetComponent<Animator>()
            .GetBoneTransform(HumanBodyBones.Hips).parent;

        SetupOutfit.SetupOutfitUI(outfit);

        var outfitRoot = outfit.GetComponent<ModularAvatarOutfitRoot>();
        var mergeArmature = outfitArmature.GetComponent<ModularAvatarMergeArmature>();
        var meshSettings = outfit.GetComponent<ModularAvatarMeshSettings>();

        Assert.DoesNotThrow(() => SetupOutfit.SetupOutfitUI(outfit));

        Assert.AreSame(outfitRoot, outfit.GetComponent<ModularAvatarOutfitRoot>());
        Assert.AreSame(mergeArmature,
            outfitArmature.GetComponent<ModularAvatarMergeArmature>());
        Assert.AreSame(meshSettings, outfit.GetComponent<ModularAvatarMeshSettings>());
        Assert.AreEqual(1, outfit.GetComponents<ModularAvatarOutfitRoot>().Length);
        Assert.AreEqual(1,
            outfitArmature.GetComponents<ModularAvatarMergeArmature>().Length);
        Assert.AreEqual(1, outfit.GetComponents<ModularAvatarMeshSettings>().Length);
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

        var mergeArmature = outfit_armature.GetComponent<ModularAvatarMergeArmature>();
        Assert.AreSame(root_armature, mergeArmature.mergeTargetObject);
        Assert.AreSame(root_chest, outfit_upperchest.GetComponent<ModularAvatarMergeArmature>().mergeTargetObject);
        Assert.AreSame(outfit_armature.transform, outfit.GetComponent<ModularAvatarOutfitRoot>().armatureRoot);
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
        var outfitRoot = outfit.AddComponent<ModularAvatarOutfitRoot>();

        SetupOutfit.SetupOutfitUI(outfit);

        var mergeArmature = outfit_armature.GetComponent<ModularAvatarMergeArmature>();
        Assert.AreEqual(root_armature, mergeArmature.mergeTargetObject);
        Assert.AreEqual(root_hips.name, outfit_hips.name);
        Assert.AreSame(outfit_armature.transform, outfitRoot.armatureRoot);
    }

    [Test]
    public void SetupOutfitAddsOutfitRootToLegacySetupWithoutChangingExistingSettings()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);

        var avatarAnimator = root.GetComponent<Animator>();
        var avatarArmature = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips).parent;
        var avatarChest = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);

        var outfit = CreateCommonPrefab("shapell.fbx");
        outfit.transform.SetParent(root.transform);
        var outfitArmature = outfit.GetComponent<Animator>()
            .GetBoneTransform(HumanBodyBones.Hips).parent;

        var mergeArmature = outfitArmature.gameObject.AddComponent<ModularAvatarMergeArmature>();
        mergeArmature.mergeTarget.Set(avatarArmature.gameObject);
        mergeArmature.prefix = "legacy-prefix-";
        mergeArmature.suffix = "-legacy-suffix";
        mergeArmature.LockMode = ArmatureLockMode.NotLocked;
        mergeArmature.mangleNames = false;
        mergeArmature.legacyLocked = true;
        mergeArmature.ResetArmatureLock();

        var meshSettings = outfit.AddComponent<ModularAvatarMeshSettings>();
        meshSettings.InheritProbeAnchor = ModularAvatarMeshSettings.InheritMode.Set;
        meshSettings.ProbeAnchor = new AvatarObjectReference(avatarChest.gameObject);
        meshSettings.InheritBounds = ModularAvatarMeshSettings.InheritMode.DontSet;
        meshSettings.RootBone = new AvatarObjectReference(avatarArmature.gameObject);
        meshSettings.Bounds = new Bounds(new Vector3(1, 2, 3), new Vector3(4, 5, 6));

        Assert.IsNull(outfit.GetComponent<ModularAvatarOutfitRoot>());
        var mergeArmatureSettings = EditorJsonUtility.ToJson(mergeArmature);
        var existingMeshSettings = EditorJsonUtility.ToJson(meshSettings);

        SetupOutfit.SetupOutfitUI(outfit);

        var outfitRoot = outfit.GetComponent<ModularAvatarOutfitRoot>();
        Assert.NotNull(outfitRoot);
        Assert.AreSame(outfitArmature, outfitRoot.armatureRoot);
        Assert.AreEqual(1, outfit.GetComponents<ModularAvatarOutfitRoot>().Length);

        Assert.AreSame(mergeArmature,
            outfitArmature.GetComponent<ModularAvatarMergeArmature>());
        Assert.AreEqual(1,
            outfitArmature.GetComponents<ModularAvatarMergeArmature>().Length);
        Assert.AreEqual(mergeArmatureSettings, EditorJsonUtility.ToJson(mergeArmature),
            "Setup Outfit changed the existing Merge Armature settings");

        Assert.AreSame(meshSettings, outfit.GetComponent<ModularAvatarMeshSettings>());
        Assert.AreEqual(1, outfit.GetComponents<ModularAvatarMeshSettings>().Length);
        Assert.AreEqual(existingMeshSettings, EditorJsonUtility.ToJson(meshSettings),
            "Setup Outfit changed the existing Mesh Settings configuration");
    }

    [Test]
    public void OutfitRootPositionResetAffectsAllMergeArmatures()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);

        var animator = root.GetComponent<Animator>();
        var avatarArmature = animator.GetBoneTransform(HumanBodyBones.Hips).parent;
        var avatarChest = animator.GetBoneTransform(HumanBodyBones.Chest);

        var outfit = CreateChild(root, "Outfit");
        var outfitArmature = CreateChild(outfit, "Armature");
        var nestedMergeRoot = CreateChild(outfitArmature, "NestedMergeRoot");

        var rootMerge = outfitArmature.AddComponent<ModularAvatarMergeArmature>();
        rootMerge.mergeTarget.Set(avatarArmature.gameObject);
        rootMerge.LockMode = ArmatureLockMode.NotLocked;
        rootMerge.ResetArmatureLock();
        var nestedMerge = nestedMergeRoot.AddComponent<ModularAvatarMergeArmature>();
        nestedMerge.mergeTarget.Set(avatarChest.gameObject);
        nestedMerge.LockMode = ArmatureLockMode.NotLocked;
        nestedMerge.ResetArmatureLock();

        outfitArmature.transform.position = Vector3.one * 100;
        outfitArmature.transform.localRotation = Quaternion.Euler(10, 20, 30);
        outfitArmature.transform.localScale = Vector3.one * 2;
        nestedMergeRoot.transform.position = Vector3.one * -100;
        nestedMergeRoot.transform.localRotation = Quaternion.Euler(40, 50, 60);
        nestedMergeRoot.transform.localScale = Vector3.one * 3;

        var options = new MergeArmaturePositionResetOptions
        {
            ConvertATPose = false,
            HeuristicRootScale = false,
            AdjustRotation = true,
            AdjustScale = true
        };
        MergeArmatureInspectorTools.ForcePositionToBaseAvatar(outfitArmature.transform, options);

        const float vectorTolerance = 0.0001f;
        const float rotationTolerance = 0.0001f;

        Assert.LessOrEqual(
            Vector3.Distance(avatarArmature.position, outfitArmature.transform.position),
            vectorTolerance);
        Assert.LessOrEqual(
            Quaternion.Angle(avatarArmature.localRotation, outfitArmature.transform.localRotation),
            rotationTolerance);
        Assert.LessOrEqual(
            Vector3.Distance(avatarArmature.localScale, outfitArmature.transform.localScale),
            vectorTolerance);
        Assert.LessOrEqual(
            Vector3.Distance(avatarChest.position, nestedMergeRoot.transform.position),
            vectorTolerance);
        Assert.LessOrEqual(
            Quaternion.Angle(avatarChest.localRotation, nestedMergeRoot.transform.localRotation),
            rotationTolerance);
        Assert.LessOrEqual(
            Vector3.Distance(avatarChest.localScale, nestedMergeRoot.transform.localScale),
            vectorTolerance);
    }

    [Test]
    public void OutfitRootScaleAdjusterMatchingAddsUpdatesAndRemovesComponents()
    {
        var root = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(root);

        var animator = root.GetComponent<Animator>();
        var avatarArmature = animator.GetBoneTransform(HumanBodyBones.Hips).parent;
        var avatarChest = animator.GetBoneTransform(HumanBodyBones.Chest);

        var outfit = CreateChild(root, "Outfit");
        var outfitArmature = CreateChild(outfit, "Armature");
        var nestedMergeRoot = CreateChild(outfitArmature, "NestedMergeRoot");
        var avatarRemovalBone = CreateChild(avatarChest.gameObject, "ScaleAdjusterRemovalBone");
        var outfitRemovalBone = CreateChild(nestedMergeRoot, "ScaleAdjusterRemovalBone");

        var rootMerge = outfitArmature.AddComponent<ModularAvatarMergeArmature>();
        rootMerge.mergeTarget.Set(avatarArmature.gameObject);
        rootMerge.LockMode = ArmatureLockMode.NotLocked;
        rootMerge.ResetArmatureLock();
        var nestedMerge = nestedMergeRoot.AddComponent<ModularAvatarMergeArmature>();
        nestedMerge.mergeTarget.Set(avatarChest.gameObject);
        nestedMerge.LockMode = ArmatureLockMode.NotLocked;
        nestedMerge.ResetArmatureLock();

        var avatarRootAdjuster = avatarArmature.gameObject.AddComponent<ModularAvatarScaleAdjuster>();
        avatarRootAdjuster.Scale = new Vector3(1.1f, 1.2f, 1.3f);
        var outfitRootAdjuster = outfitArmature.AddComponent<ModularAvatarScaleAdjuster>();
        var originalOutfitRootScale = new Vector3(2.1f, 2.2f, 2.3f);
        outfitRootAdjuster.Scale = originalOutfitRootScale;

        var avatarNestedAdjuster = avatarChest.gameObject.AddComponent<ModularAvatarScaleAdjuster>();
        avatarNestedAdjuster.Scale = new Vector3(3.1f, 3.2f, 3.3f);
        var outfitRemovalAdjuster = outfitRemovalBone.AddComponent<ModularAvatarScaleAdjuster>();
        outfitRemovalAdjuster.Scale = new Vector3(4.1f, 4.2f, 4.3f);
        Assert.IsNull(avatarRemovalBone.GetComponent<ModularAvatarScaleAdjuster>());

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        MergeArmatureInspectorTools.MatchScaleAdjusters(outfitArmature.transform);
        Undo.CollapseUndoOperations(undoGroup);

        Assert.AreSame(outfitRootAdjuster,
            outfitArmature.GetComponent<ModularAvatarScaleAdjuster>());
        Assert.AreEqual(avatarRootAdjuster.Scale, outfitRootAdjuster.Scale);
        Assert.AreEqual(avatarNestedAdjuster.Scale,
            nestedMergeRoot.GetComponent<ModularAvatarScaleAdjuster>().Scale);
        Assert.IsNull(outfitRemovalBone.GetComponent<ModularAvatarScaleAdjuster>());

        Undo.PerformUndo();

        Assert.AreEqual(originalOutfitRootScale, outfitRootAdjuster.Scale);
        Assert.IsNull(nestedMergeRoot.GetComponent<ModularAvatarScaleAdjuster>());
        Assert.NotNull(outfitRemovalBone.GetComponent<ModularAvatarScaleAdjuster>());
    }
}
