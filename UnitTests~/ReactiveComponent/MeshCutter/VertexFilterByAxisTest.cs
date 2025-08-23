using System;
using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Object = UnityEngine.Object;

public class VertexFilterByAxisTest : TestBase
{
    /*
     * Test artifacts:
     *
     * `three planes test mesh.fbx` - a mesh with three planes, aligned on the XZ axis, at positions Y=0, Y=1, and Y=-1.
     * Three blendshapes are provided, which manipulate each mesh: Positive moves the Y=1 plane, Negative moves the Y=-1
     * plane, and Center moves the Y=0 plane.
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
        avatarRoot = CreateRoot("AxisFilterTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;
        
        // Ensure we start with a consistent position and scale.
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }
    
    // Tests for vertex selection with Renderer reference frame
    [Test]
    public void TestVertexSelectionRenderer()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up; // Y-axis up
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // Only vertices above Y=0 should now be filtered
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool shouldBeFiltered = vertices[i].y > 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} filtering mismatch");
        }
    }
    
    [Test]
    public void TestVertexSelectionDifferentAxis()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.down; // Negative Y axis
        
        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool shouldBeFiltered = vertices[i].y < 0.0f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} should be filtered with negative Y axis");
        }
    }
    
    [Test]
    public void TestVertexSelectionOffsetCenter()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = new Vector3(0, -0.5f, 0); // Offset center
        component.Axis = Vector3.up;
        
        var context = new ComputeContext("test");
        var filter = new VertexFilterByAxis(component, context);
        
        // Start with all vertices unfiltered
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++) filtered[i] = false;
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        var vertices = testMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // With center at Y=0.5, only vertices above Y=0.5 should be filtered
            bool shouldBeFiltered = vertices[i].y > -0.5f;
            Assert.AreEqual(shouldBeFiltered, filtered[i], 
                $"Vertex {i} at position {vertices[i]} filtering mismatch with offset center");
        }
    }
    
    // Tests for pre-set value preservation
    [Test]
    public void TestPreFilteredVerticesRemainFiltered()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;
        
        var context = new ComputeContext("test");
        var filter = new VertexFilterByAxis(component, context);
        
        // Pre-mark all vertices as filtered
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++) filtered[i] = true;
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // All vertices should remain filtered (method only sets true, never false)
        for (int i = 0; i < filtered.Length; i++)
        {
            Assert.IsTrue(filtered[i], $"Pre-filtered vertex {i} should remain filtered");
        }
    }
    
    [Test]
    public void TestUnfilteredVerticesRemainUnfiltered()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.up * 4;
        component.Axis = Vector3.up;
        
        var context = new ComputeContext("test");
        var filter = new VertexFilterByAxis(component, context);
        
        // Mark only some vertices as filtered initially (alternating pattern)
        var filtered = new bool[testMesh.vertices.Length];
        for (int i = 0; i < filtered.Length; i++)
        {
            filtered[i] = i % 2 == 0; // Every other vertex starts as filtered
        }
        
        var originalFiltered = new bool[filtered.Length];
        System.Array.Copy(filtered, originalFiltered, filtered.Length);
        
        filter.MarkFilteredVertices(meshRenderer, testMesh, filtered);
        
        // Vertices that were originally unfiltered should remain unchanged
        for (int i = 0; i < filtered.Length; i++)
        {
            if (!originalFiltered[i])
            {
                Assert.IsFalse(filtered[i], $"Originally unfiltered vertex {i} should remain unfiltered");
            }
        }
    }
}
