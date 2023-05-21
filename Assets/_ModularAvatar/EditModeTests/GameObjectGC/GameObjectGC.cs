using System.Collections;
using System.Collections.Generic;
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
}