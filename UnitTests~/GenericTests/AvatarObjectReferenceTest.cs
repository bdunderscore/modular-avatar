using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;

public class AvatarObjectReferenceTest : TestBase
{
    [Test]
    public void whenFakeArmaturePresent_bindsToRealArmature()
    {
        var root = CreateRoot("root");
        var fake_armature = CreateChild(root, "Armature");
        var real_armature = CreateChild(root, "Armature");
        
        var hips = CreateChild(real_armature, "Hips");

        var reference = new AvatarObjectReference();
        reference.referencePath = "Armature";
        
        Assert.AreEqual(real_armature, reference.Get(root.transform));
    }
}
