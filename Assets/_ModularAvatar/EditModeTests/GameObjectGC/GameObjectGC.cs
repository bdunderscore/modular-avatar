using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class GameObjectGC : TestBase
{
    [Test]
    public void FakeHumanoidHandling()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");
        var avdesc = fake_humanoid.GetComponent<VRCAvatarDescriptor>();

        new GCGameObjectsPass(new BuildContext(avdesc), fake_humanoid).OnPreprocessAvatar();
        AvatarProcessor.ProcessAvatar(fake_humanoid);

        var animator = fake_humanoid.GetComponent<Animator>();
        Assert.NotNull(animator);
        Assert.NotNull(animator.GetBoneTransform(HumanBodyBones.Chest));
        Assert.True(fake_humanoid.transform.Find("Body") == null);
    }

    [Test]
    public void RetainEndBones()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");
        var avdesc = fake_humanoid.GetComponent<VRCAvatarDescriptor>();

        var bone1 = CreateChild(fake_humanoid, "bone1");
        var bone2 = CreateChild(bone1, "bone2.end");
        var bone3 = CreateChild(fake_humanoid, "bone2");

        new GCGameObjectsPass(new BuildContext(avdesc), fake_humanoid).OnPreprocessAvatar();
        AvatarProcessor.ProcessAvatar(fake_humanoid);

        Assert.True(bone1 != null);
        Assert.True(bone2 != null);
        Assert.True(bone3 == null);
    }
}