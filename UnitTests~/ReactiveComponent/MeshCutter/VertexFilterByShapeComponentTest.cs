using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.preview;
using Object = UnityEngine.Object;

public class VertexFilterByShapeComponentTest : TestBase
{
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
        // Test that the component can be created without errors
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();
        
        Assert.IsNotNull(component, "Component should be created successfully");
        Assert.AreEqual(0, component.Shapes.Count, "Default shape name should be empty");
        Assert.AreEqual(0.001f, component.Threshold, 0.0001f, "Default threshold should be 0.001f");
    }
    
    [Test]
    public void TestComponentProperties()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();
        
        // Test property setters and getters
        component.Shapes = new[] { "TestShape" }.ToList();
        component.Threshold = 0.5f;
        
        Assert.IsTrue(component.Shapes.SequenceEqual(new [] {"TestShape"}), "ShapeName property should work correctly");
        Assert.AreEqual(0.5f, component.Threshold, 0.0001f, "Threshold property should work correctly");
    }
    
    [Test]
    public void TestProviderCanBeCreated()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();
        component.Shapes = new [] { "Positive" }.ToList();
        component.Threshold = 0.001f;
        
        // Create a filter without context (this mimics what happens in tests)
        var filter = new VertexFilterByShape(component, ComputeContext.NullContext);
        
        Assert.IsNotNull(filter, "Filter should be created successfully");
        
        // Test filter functionality
        var filtered = new bool[testMesh.vertices.Length];
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // Check that some vertices were filtered (the Positive shape affects Z=2 plane)
        var hasFilteredVertices = filtered.Any(f => f);
        Assert.IsTrue(hasFilteredVertices, "Filter should filter some vertices for valid blendshape");
    }
    
    [Test]
    public void TestComponentMultipleBlendshapesUnion()
    {
        var component = avatarRoot.AddComponent<VertexFilterByShapeComponent>();
        component.Shapes = new[] { "Positive", "Negative" }.ToList();
        component.Threshold = 0.001f;

        var filter = new VertexFilterByShape(component, ComputeContext.NullContext);

        var filtered = new bool[testMesh.vertices.Length];
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);

        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool inPositive = Math.Abs(vertices[i].z - 2.0f) < 0.01f;
            bool inNegative = Math.Abs(vertices[i].z - 0.0f) < 0.01f;
            bool shouldBeFiltered = inPositive || inNegative;

            Assert.AreEqual(shouldBeFiltered, filtered[i],
                $"Vertex {i} at {vertices[i]} should {(shouldBeFiltered ? "" : "not ")}be filtered by shapes 'Positive' or 'Negative'");
        }
    }
}