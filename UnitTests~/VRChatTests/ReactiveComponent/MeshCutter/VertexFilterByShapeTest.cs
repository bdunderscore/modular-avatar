using System.Collections.Generic;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;

public class VertexFilterByShapeTest : TestBase
{
    private GameObject avatarRoot;
    private MeshRenderer meshRenderer;
    private Mesh testMesh;

    [SetUp]
    public override void Setup()
    {
        base.Setup();

        testMesh = CreateShapeFilterTestMesh();

        avatarRoot = CreateRoot("ShapeFilterTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;

        // Ensure we start with a consistent position and scale
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }

    private static IEnumerable<(string, bool[])> BlendShapes
    {
        get
        {
            yield return ("Positive", new[] { true, false, false, true, false });
            yield return ("Negative", new[] { false, true, false, true, false });
            yield return ("Center", new[] { false, false, true, false, false });
        }
    }

    [Test]
    public void TestVertexSelectionByValidShape(
        [ValueSource(nameof(BlendShapes))]
        (string shapeName, bool[] expectedPrimitives) shapeInfo
    )
    {
        var filter = new VertexFilterByShape(shapeInfo.shapeName, .001f);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(shapeInfo.expectedPrimitives, filtered);
    }

    [Test]
    public void TestNonExistentShape()
    {
        var filter = new VertexFilterByShape("NonExistent", 0.001f);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        for (int i = 0; i < filtered.Length; i++)
        {
            Assert.IsFalse(filtered[i],
                $"No primitives should be filtered for non-existent shape, but primitive {i} was filtered");
        }
    }

    [Test]
    public void TestUnfilteredVerticesRemainUnfiltered()
    {
        // Verify that primitives with no vertices moved by "Positive" are not marked.
        var filter = new VertexFilterByShape("Positive", 0.001f);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, false, false, true, false }, filtered);
    }

    [Test]
    public void TestEqualsAndHashCode()
    {
        var filter1 = new VertexFilterByShape("TestShape", 0.5f);
        var filter2 = new VertexFilterByShape("TestShape", 0.5f);
        var filter3 = new VertexFilterByShape("TestShape", 0.6f);
        var filter4 = new VertexFilterByShape("DifferentShape", 0.5f);

        // Test equality
        Assert.IsTrue(filter1.Equals(filter2), "Filters with same shape and threshold should be equal");
        Assert.IsFalse(filter1.Equals(filter3), "Filters with different thresholds should not be equal");
        Assert.IsFalse(filter1.Equals(filter4), "Filters with different shapes should not be equal");
        Assert.IsFalse(filter1.Equals(null), "Filter should not equal null");

        // Test hash codes
        Assert.AreEqual(filter1.GetHashCode(), filter2.GetHashCode(), "Equal filters should have same hash code");
        Assert.AreNotEqual(filter1.GetHashCode(), filter3.GetHashCode(), "Different filters should have different hash codes");
    }

    [Test]
    public void TestToString()
    {
        var filter = new VertexFilterByShape("TestShape", 0.5f);
        var result = filter.ToString();

        Assert.IsTrue(result.Contains("TestShape"), "ToString should contain the shape name");
        Assert.IsTrue(result.Contains("0.5"), "ToString should contain the threshold value");
    }

    [Test]
    public void TestMultipleBlendshapesSelectUnionOfVertices()
    {
        var filter = new VertexFilterByShape(new[] { "Positive", "Negative" }, 0.001f);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, true, false, true, false }, filtered);
    }

    [Test]
    public void TestAllVerticesSelectionMode()
    {
        var anyVertexFilter = new VertexFilterByShape("Positive", 0.001f, VertexSelectionMode.AnyVertex);
        var allVerticesFilter = new VertexFilterByShape("Positive", 0.001f, VertexSelectionMode.AllVertices);

        CollectionAssert.AreEqual(new[] { true, false, false, true, false },
            RunFilterPrimitives(anyVertexFilter, meshRenderer, testMesh));
        CollectionAssert.AreEqual(new[] { false, false, false, false, false },
            RunFilterPrimitives(allVerticesFilter, meshRenderer, testMesh));
    }

}
