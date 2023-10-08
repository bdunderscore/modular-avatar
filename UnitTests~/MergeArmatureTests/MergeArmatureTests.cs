using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

public class MergeArmatureTests : TestBase
{
    [Test]
    public void DontStripObjectsWithComponents()
    {
        var root = CreatePrefab("ColliderMergeTest.prefab");

        AvatarProcessor.ProcessAvatar(root);

        // We expect two children:  Mergable and Hips$[uuid] (retained due to the BoxCollider)
        var targetHips = root.transform.Find("TargetArmature/Hips");
        Assert.AreEqual(2, targetHips.childCount);
        Assert.AreEqual("Mergable", targetHips.GetChild(0).gameObject.name);

        Assert.NotNull(targetHips.GetChild(1).GetComponent<BoxCollider>());
    }
}