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