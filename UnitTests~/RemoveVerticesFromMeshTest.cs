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
        // Create a simple, predictable mesh to test exact triangle generation
        // This creates a small mesh that's easy to validate precisely
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt16;
        
        // Create 6 vertices forming 2 triangles
        var vertices = new Vector3[]
        {
            new Vector3(0, 0, 0),  // vertex 0
            new Vector3(1, 0, 0),  // vertex 1  
            new Vector3(0, 1, 0),  // vertex 2
            new Vector3(2, 0, 0),  // vertex 3
            new Vector3(3, 0, 0),  // vertex 4
            new Vector3(2, 1, 0)   // vertex 5
        };
        mesh.SetVertices(vertices);
        
        // Create a submesh with a high base vertex that would cause u16 overflow
        // when Unity tries to add it to triangle indices automatically
        mesh.subMeshCount = 1;
        
        // Create two triangles: [0,1,2] and [3,4,5]
        var triangles = new int[] { 0, 1, 2, 3, 4, 5 };
        
        // Set high base vertex - this is what would cause overflow in the old code
        // when GetTriangles(list, submesh, true) tried to add this to each triangle index
        var highBaseVertex = 60000;
        mesh.SetTriangles(triangles, 0, false, highBaseVertex);
        mesh.RecalculateBounds();
        
        // Create a renderer with this mesh
        var root = CreateRoot("TestMesh");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        
        // Create a filter that removes vertices 3, 4, 5 (second triangle)
        // This should leave only triangle [0,1,2] intact
        var filter = new SpecificVertexFilter(new int[] { 3, 4, 5 });
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
        
        // Verify exact triangle content
        var resultTriangles = result.GetTriangles(0);
        
        // Should have exactly 3 triangle indices (one triangle)
        Assert.AreEqual(3, resultTriangles.Length, "Should have exactly one triangle remaining");
        
        // After vertex removal and remapping, the remaining triangle should be [0,1,2]
        // since vertices 0,1,2 are kept and vertices 3,4,5 are removed
        Assert.AreEqual(0, resultTriangles[0], "First triangle vertex should be remapped to 0");
        Assert.AreEqual(1, resultTriangles[1], "Second triangle vertex should be remapped to 1");  
        Assert.AreEqual(2, resultTriangles[2], "Third triangle vertex should be remapped to 2");
        
        // Verify vertex count matches remaining vertices (only 3 vertices remain: 0,1,2)
        Assert.AreEqual(3, result.vertexCount, "Should have exactly 3 vertices remaining");
        
        // Verify vertex positions are preserved for the remaining vertices
        var resultVertices = new Vector3[result.vertexCount];
        result.GetVertices(resultVertices);
        Assert.AreEqual(new Vector3(0, 0, 0), resultVertices[0], "Original vertex 0 position should be preserved");
        Assert.AreEqual(new Vector3(1, 0, 0), resultVertices[1], "Original vertex 1 position should be preserved");
        Assert.AreEqual(new Vector3(0, 1, 0), resultVertices[2], "Original vertex 2 position should be preserved");
        
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
    public void TestExactTriangleGeneration()
    {
        // Create a simple mesh designed specifically to validate exact triangle generation
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt16;
        
        // Create a simple quad (4 vertices, 2 triangles) with predictable layout
        var vertices = new Vector3[]
        {
            new Vector3(0, 0, 0),  // vertex 0 - bottom-left
            new Vector3(1, 0, 0),  // vertex 1 - bottom-right
            new Vector3(0, 1, 0),  // vertex 2 - top-left
            new Vector3(1, 1, 0),  // vertex 3 - top-right
            new Vector3(2, 0, 0),  // vertex 4 - extra vertex to remove
            new Vector3(2, 1, 0)   // vertex 5 - extra vertex to remove
        };
        mesh.SetVertices(vertices);
        
        mesh.subMeshCount = 1;
        
        // Create triangles for the quad: [0,1,2] and [1,3,2] 
        // Plus one extra triangle [4,5,1] that will be removed
        var triangles = new int[] { 0, 1, 2, 1, 3, 2, 4, 5, 1 };
        
        // Use a moderate base vertex to test the fix without extreme values
        var baseVertex = 1000;
        mesh.SetTriangles(triangles, 0, false, baseVertex);
        mesh.RecalculateBounds();
        
        // Create a renderer with this mesh
        var root = CreateRoot("TestMesh");
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        
        // Remove vertices 4 and 5, which should remove triangle [4,5,1]
        // but preserve triangles [0,1,2] and [1,3,2]
        var filter = new SpecificVertexFilter(new int[] { 4, 5 });
        var targets = new List<(TargetProp, IVertexFilter)>
        {
            (TargetProp.Create("test"), filter)
        };
        
        var result = RemoveVerticesFromMesh.RemoveVertices(renderer, mesh, targets);
        
        // Verify the result
        Assert.IsNotNull(result);
        Assert.AreEqual(IndexFormat.UInt16, result.indexFormat);
        
        // Should have 4 vertices remaining (0,1,2,3)
        Assert.AreEqual(4, result.vertexCount, "Should have 4 vertices remaining after removing vertices 4,5");
        
        // Should have 6 triangle indices (2 triangles)
        var resultTriangles = result.GetTriangles(0);
        Assert.AreEqual(6, resultTriangles.Length, "Should have exactly 2 triangles remaining");
        
        // Verify the exact triangles - they should be [0,1,2] and [1,3,2] after remapping
        var expectedTriangles = new int[] { 0, 1, 2, 1, 3, 2 };
        for (int i = 0; i < expectedTriangles.Length; i++)
        {
            Assert.AreEqual(expectedTriangles[i], resultTriangles[i], 
                $"Triangle index {i} should be {expectedTriangles[i]} but was {resultTriangles[i]}");
        }
        
        // Verify vertex positions are preserved for remaining vertices
        var resultVertices = new Vector3[result.vertexCount];
        result.GetVertices(resultVertices);
        Assert.AreEqual(new Vector3(0, 0, 0), resultVertices[0], "Vertex 0 position preserved");
        Assert.AreEqual(new Vector3(1, 0, 0), resultVertices[1], "Vertex 1 position preserved");
        Assert.AreEqual(new Vector3(0, 1, 0), resultVertices[2], "Vertex 2 position preserved");
        Assert.AreEqual(new Vector3(1, 1, 0), resultVertices[3], "Vertex 3 position preserved");
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
    /// Test vertex filter that removes specific vertices but leaves some triangles intact
    /// </summary>
    private class TestVertexFilter : IVertexFilter
    {
        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] toDelete)
        {
            // Remove vertices in a way that leaves some triangles intact
            // For a large mesh, remove only vertices that are likely to be unused
            // or only part of some triangles, leaving some complete triangles
            if (toDelete.Length > 10)
            {
                // Remove only vertices at higher indices, leaving lower ones intact
                // This should preserve some triangles that use only the lower vertex indices
                for (int i = toDelete.Length - 3; i < toDelete.Length; i++)
                {
                    toDelete[i] = true;
                }
            }
            else
            {
                // For smaller meshes, remove vertices more sparingly
                // Only remove the last vertex if there are at least 4 vertices
                if (toDelete.Length >= 4)
                {
                    toDelete[toDelete.Length - 1] = true;
                }
            }
        }
    }
    
    /// <summary>
    /// A vertex filter that removes specific vertex indices
    /// </summary>
    private class SpecificVertexFilter : IVertexFilter
    {
        private readonly int[] _vertexIndicesToRemove;
        
        public SpecificVertexFilter(int[] vertexIndicesToRemove)
        {
            _vertexIndicesToRemove = vertexIndicesToRemove;
        }
        
        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] toDelete)
        {
            foreach (var index in _vertexIndicesToRemove)
            {
                if (index >= 0 && index < toDelete.Length)
                {
                    toDelete[index] = true;
                }
            }
        }
    }
}

#endif