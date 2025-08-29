using System;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class RemoveVerticesFromMeshTest : TestBase
{
    [Test]
    public void TestBaseVertexHandling(
        [Values(IndexFormat.UInt16, IndexFormat.UInt32)] IndexFormat indexFormat,
        [Values(true, false)] bool useBaseVertex
    )
    {
        var mesh = new Mesh();
        mesh.indexFormat = indexFormat;
        
        // We'll have two submeshes:
        //   - submesh #0: 0xFFFF vertices (just to pad things out)
        //     if no base vertex: 0x8000 vertices
        //     we'll generate whatever triangles for this one
        //   - submesh #1: Four test triangles:
        //   0x10000, 0x10001, 0x10002 - Not erased
        //   0x10000, 0x10002, 0x10003 - Erased (vertex 0x10003)
        //   0x10004, 0x10005, 0x10006 - Erased (vertex 0x10006)
        //   0x10005, 0x10002, 0x10007 - Not erased
        int n = useBaseVertex ? 0x1_0000 : 0x8000;
        mesh.vertices = Enumerable.Range(0, n + 8).Select(i => new Vector3(i, i, i)).ToArray();
        List<int> indexes = new();
        indexes.AddRange(Enumerable.Range(0,n));
        // 65536 is not divisible by 3, so pad it out to finish the first submesh
        while (indexes.Count % 3 != 0)
        {
            indexes.Add((ushort)indexes.Count);
        }
        int submesh0count = indexes.Count;
        indexes.AddRange(new int[]{
            n + 0, n + 1, n + 2,
            n + 0, n + 2, n + 3,
            n + 4, n + 5, n + 6,
            n + 5, n + 2, n + 7,
        }.Select(i =>  useBaseVertex ? i - n : i));

        mesh.subMeshCount = 2;
        mesh.SetIndices(indexes, 0, submesh0count, MeshTopology.Triangles, 0);
        mesh.SetIndices(indexes, submesh0count, indexes.Count - submesh0count, MeshTopology.Triangles, 1, baseVertex: useBaseVertex ? n : 0);

        var newMesh = RemoveVerticesFromMesh.RemoveVertices(null!, mesh, new[]
        {
            (new TargetProp(), (IVertexFilter) new SpecificVertexFilter(new int[] { n + 3, n + 6 }))
        });
        
        Assert.AreEqual(2, newMesh.subMeshCount, "Test mesh should have exactly 2 submeshes");
        
        var indexesMesh0 = new List<int>();
        var indexesMesh1 = new List<int>();
        newMesh.GetIndices(indexesMesh0, 0, true);
        newMesh.GetIndices(indexesMesh1, 1, true);
        
        Assert.AreEqual(submesh0count, indexesMesh0.Count, "First submesh triangle count should be unchanged");
        Assert.AreEqual(6, indexesMesh1.Count, "Second submesh triangle count should be reduced by 2");
        
        // Validate contents of index buffers
        Assert.That(indexesMesh0, Is.EquivalentTo(indexes.Take(submesh0count)));
        Assert.That(indexesMesh1, Is.EquivalentTo(new int[]
        {
            n + 0, n + 1, n + 2,
            // vertices 3, 4, and 6 are deleted
            n + 3, n + 2, n + 4
        }));

        var expectedVertices = mesh.vertices.ToList();
        expectedVertices.RemoveAt(n + 6);
        expectedVertices.RemoveAt(n + 4); // deleted because it is referenced only by deleted triangles
        expectedVertices.RemoveAt(n + 3);
        var vertices = newMesh.vertices;
        for (int i = 0; i < Math.Min(expectedVertices.Count, vertices.Length); i++)
        {
            Assert.AreEqual(expectedVertices[i], vertices[i], $"Vertex {i} at {vertices[i]} should be {expectedVertices[i]} but was {vertices[i]}");
        }

        Assert.That(vertices.Length, Is.EqualTo(expectedVertices.Count), "Vertex count should match");
    }

    [Test]
    public void TestVertexDataPreservation()
    {
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        
        // Create a simple mesh with 8 vertices
        int vertexCount = 8;
        mesh.vertices = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i, i * 2, i * 3)).ToArray();
        
        // Set up all vertex data types
        mesh.normals = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i * 0.1f, i * 0.2f, i * 0.3f).normalized).ToArray();
        mesh.tangents = Enumerable.Range(0, vertexCount).Select(i => new Vector4(i * 0.4f, i * 0.5f, i * 0.6f, 1.0f)).ToArray();
        
        // Set up UV channels 0-7
        for (int uvChannel = 0; uvChannel < 8; uvChannel++)
        {
            var uvs = Enumerable.Range(0, vertexCount).Select(i => new Vector2(i * 0.1f + uvChannel, i * 0.2f + uvChannel)).ToArray();
            mesh.SetUVs(uvChannel, uvs);
        }
        
        // Set up vertex colors
        mesh.colors = Enumerable.Range(0, vertexCount).Select(i => new Color(i * 0.1f, i * 0.2f, i * 0.3f, 1.0f)).ToArray();
        
        // Set up bone weights
        var boneWeights = new BoneWeight[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            boneWeights[i] = new BoneWeight
            {
                boneIndex0 = i % 4,
                boneIndex1 = (i + 1) % 4,
                weight0 = 0.7f + i * 0.01f,
                weight1 = 0.3f - i * 0.01f
            };
        }
        mesh.boneWeights = boneWeights;
        
        // Set up blend shapes
        var deltaVertices = new Vector3[vertexCount];
        var deltaNormals = new Vector3[vertexCount];
        var deltaTangents = new Vector3[vertexCount];
        
        for (int i = 0; i < vertexCount; i++)
        {
            deltaVertices[i] = new Vector3(i * 0.5f, i * 0.6f, i * 0.7f);
            deltaNormals[i] = new Vector3(i * 0.05f, i * 0.06f, i * 0.07f);
            deltaTangents[i] = new Vector3(i * 0.08f, i * 0.09f, i * 0.1f);
        }
        
        mesh.AddBlendShapeFrame("TestShape", 100.0f, deltaVertices, deltaNormals, deltaTangents);
        
        // Create triangles using vertices 0,1,2 and 3,4,5 (keeping vertices 6,7 for deletion)
        mesh.triangles = new int[] { 0, 1, 2, 3, 4, 5, 5, 6, 7 };
        
        // Remove vertices 6 and 7
        var newMesh = RemoveVerticesFromMesh.RemoveVertices(null!, mesh, new[]
        {
            (new TargetProp(), (IVertexFilter) new SpecificVertexFilter(new int[] { 6, 7 }))
        });
        
        // Verify vertex count
        Assert.AreEqual(6, newMesh.vertexCount, "Should have 6 vertices remaining");
        
        // Verify vertices
        var expectedVertices = mesh.vertices.Take(6).ToArray();
        Assert.That(newMesh.vertices, Is.EqualTo(expectedVertices), "Vertices should be preserved");
        
        // Verify normals
        var expectedNormals = mesh.normals.Take(6).ToArray();
        Assert.That(newMesh.normals, Is.EqualTo(expectedNormals), "Normals should be preserved");
        
        // Verify tangents
        var expectedTangents = mesh.tangents.Take(6).ToArray();
        Assert.That(newMesh.tangents, Is.EqualTo(expectedTangents), "Tangents should be preserved");
        
        // Verify UV channels 0-7
        for (int uvChannel = 0; uvChannel < 8; uvChannel++)
        {
            var originalUVs = new List<Vector2>();
            mesh.GetUVs(uvChannel, originalUVs);
            var expectedUVs = originalUVs.Take(6).ToList();
            
            var newUVs = new List<Vector2>();
            newMesh.GetUVs(uvChannel, newUVs);
            
            Assert.That(newUVs, Is.EqualTo(expectedUVs), $"UV channel {uvChannel} should be preserved");
        }
        
        // Verify vertex colors
        var expectedColors = mesh.colors.Take(6).ToArray();
        Assert.That(newMesh.colors, Is.EqualTo(expectedColors), "Vertex colors should be preserved");
        
        // Verify bone weights
        var expectedBoneWeights = mesh.boneWeights.Take(6).ToArray();
        Assert.That(newMesh.boneWeights, Is.EqualTo(expectedBoneWeights), "Bone weights should be preserved");
        
        // Verify blend shapes
        Assert.AreEqual(1, newMesh.blendShapeCount, "Should have 1 blend shape");
        Assert.AreEqual("TestShape", newMesh.GetBlendShapeName(0), "Blend shape name should be preserved");
        Assert.AreEqual(1, newMesh.GetBlendShapeFrameCount(0), "Should have 1 blend shape frame");
        Assert.AreEqual(100.0f, newMesh.GetBlendShapeFrameWeight(0, 0), "Blend shape weight should be preserved");
        
        // Get blend shape frame data
        var newDeltaVertices = new Vector3[newMesh.vertexCount];
        var newDeltaNormals = new Vector3[newMesh.vertexCount];
        var newDeltaTangents = new Vector3[newMesh.vertexCount];
        
        newMesh.GetBlendShapeFrameVertices(0, 0, newDeltaVertices, newDeltaNormals, newDeltaTangents);
        
        var expectedDeltaVertices = deltaVertices.Take(6).ToArray();
        var expectedDeltaNormals = deltaNormals.Take(6).ToArray();
        var expectedDeltaTangents = deltaTangents.Take(6).ToArray();
        
        Assert.That(newDeltaVertices, Is.EqualTo(expectedDeltaVertices), "Blend shape delta vertices should be preserved");
        Assert.That(newDeltaNormals, Is.EqualTo(expectedDeltaNormals), "Blend shape delta normals should be preserved");
        Assert.That(newDeltaTangents, Is.EqualTo(expectedDeltaTangents), "Blend shape delta tangents should be preserved");
    }

    [Test]
    public void TestHighDimensionalUVPreservation()
    {
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        
        // Create a simple mesh with 8 vertices
        int vertexCount = 8;
        mesh.vertices = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i, i * 2, i * 3)).ToArray();
        
        // Set up 3D UV channel (using channel 2)
        var uv3D = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i * 0.1f, i * 0.2f, i * 0.3f)).ToArray();
        mesh.SetUVs(2, uv3D);
        
        // Set up 4D UV channel (using channel 3)
        var uv4D = Enumerable.Range(0, vertexCount).Select(i => new Vector4(i * 0.1f, i * 0.2f, i * 0.3f, i * 0.4f)).ToArray();
        mesh.SetUVs(3, uv4D);
        
        // Create triangles using vertices 0,1,2 and 3,4,5 (keeping vertices 6,7 for deletion)
        mesh.triangles = new int[] { 0, 1, 2, 3, 4, 5, 5, 6, 7 };
        
        // Remove vertices 6 and 7
        var newMesh = RemoveVerticesFromMesh.RemoveVertices(null!, mesh, new[]
        {
            (new TargetProp(), (IVertexFilter) new SpecificVertexFilter(new int[] { 6, 7 }))
        });
        
        // Verify vertex count
        Assert.AreEqual(6, newMesh.vertexCount, "Should have 6 vertices remaining");
        
        // Verify 3D UV channel
        var originalUV3D = new List<Vector3>();
        mesh.GetUVs(2, originalUV3D);
        var expectedUV3D = originalUV3D.Take(6).ToList();
        
        var newUV3D = new List<Vector3>();
        newMesh.GetUVs(2, newUV3D);
        
        Assert.That(newUV3D, Is.EqualTo(expectedUV3D), "3D UV channel should be preserved");
        
        // Verify 4D UV channel
        var originalUV4D = new List<Vector4>();
        mesh.GetUVs(3, originalUV4D);
        var expectedUV4D = originalUV4D.Take(6).ToList();
        
        var newUV4D = new List<Vector4>();
        newMesh.GetUVs(3, newUV4D);
        
        Assert.That(newUV4D, Is.EqualTo(expectedUV4D), "4D UV channel should be preserved");
    }

    private class SpecificVertexFilter : IVertexFilter
    {
        private HashSet<int> _toDeleteVertices;
        
        public SpecificVertexFilter(IEnumerable<int> toDeleteVertices)
        {
            _toDeleteVertices = toDeleteVertices.ToHashSet();
        }

        public bool Equals(IVertexFilter other)
        {
            throw new NotImplementedException();
        }

        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            foreach (var i in _toDeleteVertices)
            {
                filtered[i] = true;
            }
        }
    }
}