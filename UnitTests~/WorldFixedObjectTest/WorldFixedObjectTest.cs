using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.Constraint.Components;
#else
using UnityEngine.Animations;
#endif


public class WorldFixedObjectTest : TestBase
{
    [Test]
    public void SimpleTest()
    {
        var avatar = CreatePrefab("Simple.prefab");

        var fixedObject = avatar.transform.Find("FixedObject");

        // initialize context
        var buildContext = new BuildContext(avatar);
        buildContext.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();

        new WorldFixedObjectProcessor().Process(buildContext);

        var fixedRoot = avatar.transform.Find("(MA WorldFixedRoot)");
        var movedFixedObject = avatar.transform.Find("(MA WorldFixedRoot)/FixedObject");

        // fixed root is created
        Assert.That(fixedRoot, Is.Not.Null);
        #if MA_VRCSDK3_AVATARS
        Assert.That(fixedRoot.GetComponent<VRCParentConstraint>(), Is.Not.Null);
        #else
        Assert.That(fixedRoot.GetComponent<ParentConstraint>(), Is.Not.Null);
        #endif

        // objects are moved to fixed root
        Assert.That(movedFixedObject, Is.Not.Null);
        Assert.That(movedFixedObject, Is.EqualTo(fixedObject));
    }

    [Test]
    public void NestedTest()
    {
        var avatar = CreatePrefab("Nested.prefab");

        var fixedObject = avatar.transform.Find("FixedObject");
        var nestedFixed = avatar.transform.Find("FixedObject/NestedFixed");

        // initialize context
        var buildContext = new BuildContext(avatar);
        buildContext.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();

        new WorldFixedObjectProcessor().Process(buildContext);

        var fixedRoot = avatar.transform.Find("(MA WorldFixedRoot)");
        var movedFixedObject = avatar.transform.Find("(MA WorldFixedRoot)/FixedObject");
        var nestedFixedObject = avatar.transform.Find("(MA WorldFixedRoot)/NestedFixed");

        // fixed root is created
        Assert.That(fixedRoot, Is.Not.Null);
#if MA_VRCSDK3_AVATARS
        Assert.That(fixedRoot.GetComponent<VRCParentConstraint>(), Is.Not.Null);
#else
        Assert.That(fixedRoot.GetComponent<ParentConstraint>(), Is.Not.Null);
#endif

        // objects are moved to fixed root
        Assert.That(movedFixedObject, Is.Not.Null);
        Assert.That(movedFixedObject, Is.EqualTo(fixedObject));

        // objects are moved to fixed root
        Assert.That(nestedFixedObject, Is.Not.Null);
        Assert.That(nestedFixedObject, Is.EqualTo(nestedFixed));
    }

    [Test]
    public void NameCollisions()
    {
        var avatar = CreateRoot("Avatar");
        var target1 = CreateChild(avatar, "Target");
        var target2 = CreateChild(avatar, "Target");

        target1.AddComponent<ModularAvatarWorldFixedObject>();
        target2.AddComponent<ModularAvatarWorldFixedObject>();
        
        // initialize context
        var buildContext = new BuildContext(avatar);
        var animationServices = buildContext.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();

        new WorldFixedObjectProcessor().Process(buildContext);
        
        Assert.AreSame(target1.transform.parent, target2.transform.parent);
        Assert.AreNotSame(target1.transform.parent, avatar.transform);
        Assert.AreNotSame(target1.gameObject.name, target2.gameObject.name);
    }
}