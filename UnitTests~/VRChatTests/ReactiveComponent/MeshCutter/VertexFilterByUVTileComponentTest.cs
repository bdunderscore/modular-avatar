using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.vertex_filters;

public class VertexFilterByUVTileComponentTest : TestBase
{
    private GameObject avatarRoot;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        avatarRoot = CreateRoot("UVTileFilterComponentTestObject");
    }

    [Test]
    public void TestComponentCanBeCreated()
    {
        var component = avatarRoot.AddComponent<VertexFilterByUVTileComponent>();

        Assert.IsNotNull(component, "Component should be created successfully");
        Assert.AreEqual(0, component.UVChannel, "Default UVChannel should be 0");
        Assert.IsFalse(component.UseUMin, "Default UseUMin should be false");
        Assert.IsFalse(component.UMinInclusive, "Default UMinInclusive should be false");
        Assert.IsFalse(component.UseUMax, "Default UseUMax should be false");
        Assert.IsFalse(component.UMaxInclusive, "Default UMaxInclusive should be false");
        Assert.IsFalse(component.UseVMin, "Default UseVMin should be false");
        Assert.IsFalse(component.VMinInclusive, "Default VMinInclusive should be false");
        Assert.IsFalse(component.UseVMax, "Default UseVMax should be false");
        Assert.IsFalse(component.VMaxInclusive, "Default VMaxInclusive should be false");
        Assert.AreEqual(0f, component.UMin, "Default UMin should be 0");
        Assert.AreEqual(1f, component.UMax, "Default UMax should be 1");
        Assert.AreEqual(0f, component.VMin, "Default VMin should be 0");
        Assert.AreEqual(1f, component.VMax, "Default VMax should be 1");
        Assert.IsFalse(component.Invert, "Default Invert should be false");
        Assert.AreEqual(VertexSelectionMode.AnyVertex, component.SelectionMode,
            "Default SelectionMode should be AnyVertex");
    }

    [Test]
    public void TestComponentProperties()
    {
        var component = avatarRoot.AddComponent<VertexFilterByUVTileComponent>();

        component.UVChannel = 2;
        component.UseUMin = true;
        component.UMinInclusive = true;
        component.UMin = 0.25f;
        component.UseUMax = true;
        component.UMaxInclusive = true;
        component.UMax = 0.75f;
        component.UseVMin = true;
        component.VMinInclusive = true;
        component.VMin = 0.1f;
        component.UseVMax = true;
        component.VMaxInclusive = true;
        component.VMax = 0.9f;
        component.Invert = true;
        component.SelectionMode = VertexSelectionMode.AllVertices;

        Assert.AreEqual(2, component.UVChannel);
        Assert.IsTrue(component.UseUMin);
        Assert.IsTrue(component.UMinInclusive);
        Assert.AreEqual(0.25f, component.UMin);
        Assert.IsTrue(component.UseUMax);
        Assert.IsTrue(component.UMaxInclusive);
        Assert.AreEqual(0.75f, component.UMax);
        Assert.IsTrue(component.UseVMin);
        Assert.IsTrue(component.VMinInclusive);
        Assert.AreEqual(0.1f, component.VMin);
        Assert.IsTrue(component.UseVMax);
        Assert.IsTrue(component.VMaxInclusive);
        Assert.AreEqual(0.9f, component.VMax);
        Assert.IsTrue(component.Invert);
        Assert.AreEqual(VertexSelectionMode.AllVertices, component.SelectionMode);
    }
}