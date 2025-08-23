using System;
using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using Object = UnityEngine.Object;

public class VertexFilterByShapeTest : TestBase
{
    /*
     * Test artifacts:
     *
     * `three planes test mesh.fbx` - a mesh with three planes, aligned on the XY axis, at positions Z=0, Z=1, and Z=2.
     * Three blendshapes are provided, which manipulate each mesh: Positive moves the Z=2 plane, Negative moves the Z=0
     * plane, and Center moves the Z=1 plane.
     */
    
    private GameObject avatarRoot;
    private MeshRenderer meshRenderer;
    private Mesh testMesh;
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        // Load the test mesh
        testMesh = LoadAsset<Mesh>("three planes test mesh.fbx");
        
        // Create test object with the mesh
        avatarRoot = CreateRoot("ShapeFilterTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;
        
        // Ensure we start with a consistent position and scale
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }
    
    private static IEnumerable<(float, string)> BlendShapes
    {
        get
        {
            yield return (2.0f, "Positive");
            yield return (0.0f, "Negative");
            yield return (1.0f, "Center");
        }
    }
    
    [Test]
    public void TestVertexSelectionByValidShape(
        [ValueSource(nameof(BlendShapes))]
        (float expectedY, string shapeName) shapeInfo
    )
    {
        var filter = new VertexFilterByShape(shapeInfo.shapeName, .001f);
        
        var filtered = new bool[testMesh.vertices.Length];
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        var vertices = testMesh.vertices;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            bool inPosition = Math.Abs(vertices[i].z - shapeInfo.expectedY) < 0.01f;
            
            Assert.AreEqual(inPosition, filtered[i],
                $"Vertex {i} at {vertices[i]} should {(inPosition ? "" : "not ")}be filtered by shape '{shapeInfo.shapeName}'");
        }
    }
    
    [Test]
    public void TestNonExistentShape()
    {
        // Test filtering with a non-existent blendshape
        var filter = new VertexFilterByShape("NonExistent", 0.001f);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // No vertices should be filtered since the shape doesn't exist
        for (int i = 0; i < filtered.Length; i++)
        {
            Assert.IsFalse(filtered[i], $"No vertices should be filtered for non-existent shape, but vertex {i} was filtered");
        }
    }
    
    [Test]
    public void TestUnfilteredVerticesRemainUnfiltered()
    {
        var filter = new VertexFilterByShape("Positive", 0.001f);
        
        // Mark only some vertices as filtered initially
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++)
        {
            filtered[i] = i % 2 == 0; // Every other vertex starts as filtered
        }
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        var vertices = testMesh.vertices;
        for (int i = 0; i < filtered.Length; i++)
        {
            bool shouldBeSelected = (i % 2 == 0) || (vertices[i].z > 1.5f);
            Assert.AreEqual(shouldBeSelected, filtered[i],
                $"Vertex {i} at {vertices[i]} should {(shouldBeSelected ? "" : "not ")}be filtered by shape 'Positive'");
        }
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
}
