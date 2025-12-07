using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

using UnityObject = UnityEngine.Object;

public class ArmatureConfusionTest : TestBase
{
    [TearDown]
    public void TearDown()
    {
        ESOErrorWindow.Suppress = false;
    }

    [Test]
    public void TestArmatureConfusionWorkaround()
    {
        ESOErrorWindow.Suppress = true;

        // Arrange for a confused armature
        var outer = CreateCommonPrefab("shapell.fbx");
        var inner = CreateCommonPrefab("shapell.fbx");

        var outerAnimator = outer.GetComponent<Animator>();
#if MA_VRCSDK3_AVATARS
        outer.AddComponent<VRCAvatarDescriptor>();
#endif

        inner.gameObject.name = "inner";
        inner.transform.parent = outer.transform;

        // Unity seems to determine which armature is the "true" armature by counting the number of bones that match
        // the humanoid description, and finding the root which has the most matches. Let's confuse it a bit by removing
        // some non-humanoid bones from the outer armature.
        var outerTarget = outer.transform.Find("Armature/Hips/Tail");
        UnityObject.DestroyImmediate(outerTarget.gameObject);

        // Clear animator cache
        var avatar = outerAnimator.avatar;
        outerAnimator.avatar = null;
        // ReSharper disable once Unity.InefficientPropertyAccess
        outerAnimator.avatar = avatar;

        // Verify that we're well and confused now
        Assert.AreSame(
            outerAnimator.GetBoneTransform(HumanBodyBones.Hips),
            inner.transform.Find("Armature/Hips")
        );

        // Now do a setup outfit operation
        Selection.activeGameObject = inner;
        SetupOutfit.SetupOutfitUI(inner);

        // Verify that we're not confused anymore
        Assert.AreSame(
            outerAnimator.GetBoneTransform(HumanBodyBones.Hips),
            outer.transform.Find("Armature/Hips")
        );
    }
}