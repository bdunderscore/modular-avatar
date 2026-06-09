using System.Linq;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;

public class VertexFilterByShapeComponentTest : TestBase
{
    private GameObject avatarRoot;
    private MeshRenderer meshRenderer;
    private Mesh testMesh;

    [SetUp]
    public override void Setup()
    {
        base.Setup();

        testMesh = CreateShapeFilterTestMesh();

        avatarRoot = CreateRoot("ShapeFilterComponentTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;

        // Ensure we start with a consistent position and scale
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }

    [Test]
    public void TestComponentCanBeCreated()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();

        Assert.IsNotNull(component, "Component should be created successfully");
        Assert.AreEqual(0, component.Shapes.Count, "Default shape name should be empty");
        Assert.AreEqual(0.001f, component.Threshold, 0.0001f, "Default threshold should be 0.001f");
    }

    [Test]
    public void TestComponentProperties()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();

        component.Shapes = new[] { "TestShape" }.ToList();
        component.Threshold = 0.5f;

        Assert.IsTrue(component.Shapes.SequenceEqual(new[] { "TestShape" }), "ShapeName property should work correctly");
        Assert.AreEqual(0.5f, component.Threshold, 0.0001f, "Threshold property should work correctly");
    }

    [Test]
    public void TestProviderCanBeCreated()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();
        component.Shapes = new[] { "Positive" }.ToList();
        component.Threshold = 0.001f;

        var filter = new VertexFilterByShape(component, ComputeContext.NullContext);

        Assert.IsNotNull(filter, "Filter should be created successfully");

        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);
        CollectionAssert.AreEqual(new[] { true, false, false, true, false }, filtered);
    }

    [Test]
    public void TestComponentMultipleBlendshapesUnion()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();
        component.Shapes = new[] { "Positive", "Negative" }.ToList();
        component.Threshold = 0.001f;

        var filter = new VertexFilterByShape(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, true, false, true, false }, filtered);
    }

}
