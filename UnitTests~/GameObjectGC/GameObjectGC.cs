using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

public class GameObjectGC : TestBase
{
    [Test]
    public void FakeHumanoidHandling()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");
        var context = new BuildContext(fake_humanoid);

        new GCGameObjectsPass(context, fake_humanoid).OnPreprocessAvatar();
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
        var context = new BuildContext(fake_humanoid);

        var bone1 = CreateChild(fake_humanoid, "bone1");
        var bone2 = CreateChild(bone1, "bone2.end");
        var bone3 = CreateChild(fake_humanoid, "bone2");

        new GCGameObjectsPass(context, fake_humanoid).OnPreprocessAvatar();
        AvatarProcessor.ProcessAvatar(fake_humanoid);

        Assert.True(bone1 != null);
        Assert.True(bone2 != null);
        Assert.True(bone3 == null);
    }

    [Test]
    public void RetainArmatureHack()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");
        var context = new BuildContext(fake_humanoid);

        var armature = new GameObject();
        armature.name = "Armature";
        armature.transform.parent = fake_humanoid.transform;
        armature.transform.SetSiblingIndex(0);
        
        new GCGameObjectsPass(context, fake_humanoid).OnPreprocessAvatar();
        AvatarProcessor.ProcessAvatar(fake_humanoid);

        Assert.AreEqual(2,
            context.AvatarRootObject.GetComponentsInChildren<Transform>().Count(t => t.gameObject.name == "Armature"));
    }
}