#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class RemoveVerticesFromMeshTest : TestBase
{
    [Test]
    public void TestU16IndexFormatWithHighBaseVertex()
    {
        // Create a test case that specifically reproduces the u16 overflow scenario
        // by creating a mesh with a high base vertex that would cause overflow when
        // added to triangle indices in u16 arithmetic
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt16;
        
        // Create enough vertices to have realistic mesh size
        var vertices = new List<Vector3>();
        for (int i = 0; i < 70000; i++)
        {
            vertices.Add(new Vector3(i % 100, (i / 100) % 100, (i / 10000) % 10));
        }
        mesh.SetVertices(vertices);
        
        // Create a submesh with a high base vertex that would cause u16 overflow
        // when Unity tries to add it to triangle indices automatically
        mesh.subMeshCount = 1;
        
        // Use triangle indices that when combined with a high base vertex
        // would exceed 65535 (u16 limit)
        var triangles = new List<int>();
        for (int i = 0; i < 600; i += 3)
        {
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }
        
        // Set high base vertex - this is what would cause overflow in the old code
        // when GetTriangles(list, submesh, true) tried to add this to each triangle index
        var highBaseVertex = 60000;
        mesh.SetTriangles(triangles, 0, false, highBaseVertex);
        mesh.RecalculateBounds();
        
        // Create a renderer with this mesh
        var root = CreateRoot("TestMesh");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        
        // Create a filter that removes some vertices
        var filter = new TestVertexFilter();
        var targets = new List<(TargetProp, IVertexFilter)>
        {
            (TargetProp.Create("test"), filter)
        };
        
        // This should not throw an exception or produce incorrect results
        // The old code would have issues here due to u16 overflow when calling
        // GetTriangles with automatic base vertex correction
        var result = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, targets);
        
        // Verify the result
        Assert.IsNotNull(result);
        Assert.AreEqual(IndexFormat.UInt16, result.indexFormat);
        Assert.Greater(result.vertexCount, 0);
        
        // Verify triangles are valid
        var resultTriangles = result.GetTriangles(0);
        foreach (var index in resultTriangles)
        {
            Assert.GreaterOrEqual(index, 0, "Triangle index should be non-negative");
            Assert.Less(index, result.vertexCount, "Triangle index should be within vertex count");
        }
        
        // Verify that we properly handle the base vertex
        Assert.GreaterOrEqual(result.GetBaseVertex(0), 0, "Base vertex should be non-negative");
    }
    
    [Test]
    public void TestU32IndexFormatHandling()
    {
        // Create a mesh with u32 index format for comparison
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        
        // Create vertices
        var vertices = new List<Vector3>();
        for (int i = 0; i < 1000; i++)
        {
            vertices.Add(new Vector3(i, 0, 0));
        }
        mesh.SetVertices(vertices);
        
        // Create triangles
        var triangles = new List<int>();
        for (int i = 0; i < 900; i += 3)
        {
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }
        
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        
        // Create a renderer with this mesh
        var root = CreateRoot("TestMesh");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        
        // Create a filter that removes some vertices
        var filter = new TestVertexFilter();
        var targets = new List<(TargetProp, IVertexFilter)>
        {
            (TargetProp.Create("test"), filter)
        };
        
        // This should work fine
        var result = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, targets);
        
        // Verify the result
        Assert.IsNotNull(result);
        Assert.AreEqual(IndexFormat.UInt32, result.indexFormat);
        Assert.Greater(result.vertexCount, 0);
        
        // Verify triangles are valid
        var resultTriangles = result.GetTriangles(0);
        foreach (var index in resultTriangles)
        {
            Assert.GreaterOrEqual(index, 0);
            Assert.Less(index, result.vertexCount);
        }
    }

    [Test]
    public void TestEmptySubmeshHandling()
    {
        // Test that we properly handle submeshes where all triangles are removed
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt16;
        
        // Create few vertices
        var vertices = new List<Vector3>
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0)
        };
        mesh.SetVertices(vertices);
        
        // Create one triangle
        var triangles = new List<int> { 0, 1, 2 };
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        
        // Create a renderer
        var root = CreateRoot("TestMesh");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        
        // Create a filter that removes ALL vertices
        var filter = new AllVerticesFilter();
        var targets = new List<(TargetProp, IVertexFilter)>
        {
            (TargetProp.Create("test"), filter)
        };
        
        // This should create a degenerate triangle rather than fail
        var result = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, targets);
        
        // Verify the result
        Assert.IsNotNull(result);
        Assert.AreEqual(IndexFormat.UInt16, result.indexFormat);
        Assert.AreEqual(1, result.vertexCount); // Should have kept one vertex
        
        // Should have created a degenerate triangle
        var resultTriangles = result.GetTriangles(0);
        Assert.AreEqual(3, resultTriangles.Length);
        Assert.AreEqual(0, resultTriangles[0]);
        Assert.AreEqual(0, resultTriangles[1]);
        Assert.AreEqual(0, resultTriangles[2]);
    }

    /// <summary>
    /// Test vertex filter that removes all vertices
    /// </summary>
    private class AllVerticesFilter : IVertexFilter
    {
        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] toDelete)
        {
            // Mark all vertices for deletion
            for (int i = 0; i < toDelete.Length; i++)
            {
                toDelete[i] = true;
            }
        }
    }

    /// <summary>
    /// Test vertex filter that removes every other vertex (creates gaps in vertex indices)
    /// </summary>
    private class TestVertexFilter : IVertexFilter
    {
        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] toDelete)
        {
            // Remove every other vertex to create gaps
            for (int i = 0; i < toDelete.Length; i += 2)
            {
                toDelete[i] = true;
            }
        }
    }
}

#endif