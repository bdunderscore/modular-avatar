using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

public class WorldFixedObjectTest : TestBase
{
    [Test]
    public void SimpleTest()
    {
        var avatar = CreatePrefab("Simple.prefab");
        var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();

        var fixedObject = avatar.transform.Find("FixedObject");
        
        // initialize context
        var buildContext = new BuildContext(descriptor);

        new WorldFixedObjectProcessor(descriptor).Process(buildContext);

        var fixedRoot = avatar.transform.Find("(MA WorldFixedRoot)");
        var movedFixedObject = avatar.transform.Find("(MA WorldFixedRoot)/FixedObject");

        // fixed root is created
        Assert.That(fixedRoot, Is.Not.Null);
        Assert.That(fixedRoot.GetComponent<ParentConstraint>(), Is.Not.Null);

        // objects are moved to fixed root
        Assert.That(movedFixedObject, Is.Not.Null);
        Assert.That(movedFixedObject, Is.EqualTo(fixedObject));
    }
    
    [Test]
    public void NestedTest()
    {
        var avatar = CreatePrefab("Nested.prefab");
        var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();

        var fixedObject = avatar.transform.Find("FixedObject");
        var nestedFixed = avatar.transform.Find("FixedObject/NestedFixed");
        
        // initialize context
        var buildContext = new BuildContext(descriptor);

        new WorldFixedObjectProcessor(descriptor).Process(buildContext);

        var fixedRoot = avatar.transform.Find("(MA WorldFixedRoot)");
        var movedFixedObject = avatar.transform.Find("(MA WorldFixedRoot)/FixedObject");
        var nestedFixedObject = avatar.transform.Find("(MA WorldFixedRoot)/NestedFixed");

        // fixed root is created
        Assert.That(fixedRoot, Is.Not.Null);
        Assert.That(fixedRoot.GetComponent<ParentConstraint>(), Is.Not.Null);

        // objects are moved to fixed root
        Assert.That(movedFixedObject, Is.Not.Null);
        Assert.That(movedFixedObject, Is.EqualTo(fixedObject));

        // objects are moved to fixed root
        Assert.That(nestedFixedObject, Is.Not.Null);
        Assert.That(nestedFixedObject, Is.EqualTo(nestedFixed));
    }
}
