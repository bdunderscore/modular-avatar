using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

public class GameObjectGC : TestBase
{
    
    private GameObject CreateTestHumanoid()
    {
        var obj = CreateCommonPrefab("shapell.fbx");
        AddMinimalAvatarComponents(obj);

        return obj;
    }
    
    [Test]
    public void FakeHumanoidHandling()
    {
        var fake_humanoid = CreateTestHumanoid();
        var context = new BuildContext(fake_humanoid);

        new GameObject("test").transform.parent = fake_humanoid.transform;

        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        
        var state = context.PluginBuildContext.GetState<GCGameObjectsPassState>();
        state.Context = context;
        state.Root = fake_humanoid;
        new GCGameObjectsPass().OnPreprocessAvatar(state);
        
        var animator = fake_humanoid.GetComponent<Animator>();
        Assert.NotNull(animator);
        Assert.NotNull(animator.GetBoneTransform(HumanBodyBones.Chest));
        Assert.Null(fake_humanoid.transform.Find("test"));
    }

    [Test]
    public void RetainEndBones()
    {
        var fake_humanoid = CreateTestHumanoid();
        var context = new BuildContext(fake_humanoid);

        var bone1 = CreateChild(fake_humanoid, "bone1");
        var bone2 = CreateChild(bone1, "bone2.end");
        var bone3 = CreateChild(fake_humanoid, "bone2");

        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        
        var state = context.PluginBuildContext.GetState<GCGameObjectsPassState>();
        state.Context = context;
        state.Root = fake_humanoid;
        new GCGameObjectsPass().OnPreprocessAvatar(state);

        Assert.True(bone1 != null);
        Assert.True(bone2 != null);
        Assert.True(bone3 == null);
    }

    [Test]
    public void RetainArmatureHack()
    {
        var fake_humanoid = CreateTestHumanoid();

        var armature = new GameObject();
        armature.name = "Armature";
        armature.transform.parent = fake_humanoid.transform;
        armature.transform.SetSiblingIndex(0);
        
        var context = new BuildContext(fake_humanoid);
        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        
        var state = context.PluginBuildContext.GetState<GCGameObjectsPassState>();
        state.Context = context;
        state.Root = fake_humanoid;
        new GCGameObjectsPass().OnPreprocessAvatar(state);

        Assert.AreEqual(2,
            fake_humanoid.GetComponentsInChildren<Transform>().Count(t => t.gameObject.name == "Armature"));
    }

    [Test]
    public void AnimatedObjectsAreRetained()
    {
        #if !MA_VRCSDK3_AVATARS
        Assert.Ignore("VRC-specific test");
        #endif
        var prefab = CreatePrefab("WithAnimatedObject.prefab");

        var context = new BuildContext(prefab);
        context.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        
        var state = context.PluginBuildContext.GetState<GCGameObjectsPassState>();
        state.Context = context;
        state.Root = prefab;
        new GCGameObjectsPass().OnPreprocessAvatar(state);
        
        Assert.IsTrue(prefab.transform.Find("x") != null);
    }
}