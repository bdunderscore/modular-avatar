using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

public class GameObjectGC : TestBase
{
    [Test]
    public void FakeHumanoidHandling()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");
        var context = new BuildContext(fake_humanoid);

        new GameObject("test").transform.parent = fake_humanoid.transform;

        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        new GCGameObjectsPass(context, fake_humanoid).OnPreprocessAvatar();
        
        var animator = fake_humanoid.GetComponent<Animator>();
        Assert.NotNull(animator);
        Assert.NotNull(animator.GetBoneTransform(HumanBodyBones.Chest));
        Assert.Null(fake_humanoid.transform.Find("test"));
    }

    [Test]
    public void RetainEndBones()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");
        var context = new BuildContext(fake_humanoid);

        var bone1 = CreateChild(fake_humanoid, "bone1");
        var bone2 = CreateChild(bone1, "bone2.end");
        var bone3 = CreateChild(fake_humanoid, "bone2");

        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        new GCGameObjectsPass(context, fake_humanoid).OnPreprocessAvatar();

        Assert.True(bone1 != null);
        Assert.True(bone2 != null);
        Assert.True(bone3 == null);
    }

    [Test]
    public void RetainArmatureHack()
    {
        var fake_humanoid = CreatePrefab("FakeHumanoid.prefab");

        var armature = new GameObject();
        armature.name = "Armature";
        armature.transform.parent = fake_humanoid.transform;
        armature.transform.SetSiblingIndex(0);
        
        var context = new BuildContext(fake_humanoid);
        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        new GCGameObjectsPass(context, fake_humanoid).OnPreprocessAvatar();

        Assert.AreEqual(2,
            fake_humanoid.GetComponentsInChildren<Transform>().Count(t => t.gameObject.name == "Armature"));
    }

    [Test]
    public void AnimatedObjectsAreRetained()
    {
        var prefab = CreatePrefab("WithAnimatedObject.prefab");

        var context = new BuildContext(prefab);
        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        new GCGameObjectsPass(context, prefab).OnPreprocessAvatar();
        
        Assert.IsTrue(prefab.transform.Find("x") != null);
    }
}