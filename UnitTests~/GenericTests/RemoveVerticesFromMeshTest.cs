using System;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
#if UNITY_6000_2_OR_NEWER
using UnityEditor;
#endif
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
        indexes.AddRange(Enumerable.Range(0, n));
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
        }.Select(i => useBaseVertex ? i - n : i));

        mesh.subMeshCount = 2;
        mesh.SetIndices(indexes, 0, submesh0count, MeshTopology.Triangles, 0);
        mesh.SetIndices(indexes, submesh0count, indexes.Count - submesh0count, MeshTopology.Triangles, 1, baseVertex: useBaseVertex ? n : 0);

        var newMesh = RemoveVerticesFromMesh.RemoveVertices(null!, mesh, new[]
        {
            (new TargetProp(), (IMeshSelector) new SpecificVertexFilter(new int[] { n + 3, n + 6 }))
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

#if UNITY_6000_2_OR_NEWER
    [Test]
    public void TestMeshSelectorJobWithMeshLods(
        [Values(IndexFormat.UInt16, IndexFormat.UInt32)] IndexFormat indexFormat
    )
    {
        var mesh = CreateMeshWithLods(indexFormat);
        const int selectedVertex = 40;
        var lod0Indices = mesh.GetIndices(0, 0);
        var primitiveCount = lod0Indices.Length / 3;

        using var selectorJob = new MeshSelectorJob(null!, mesh);
        using var primitiveMask = new NativeArray<bool>(primitiveCount, Allocator.TempJob);
        var filter = new SpecificVertexFilter(new[] { selectedVertex });
        filter.MarkFilteredPrimitives(selectorJob, 0, primitiveMask).Complete();

        var expectedMask = Enumerable.Range(0, primitiveCount)
            .Select(primitive => lod0Indices[primitive * 3] == selectedVertex
                                 || lod0Indices[primitive * 3 + 1] == selectedVertex
                                 || lod0Indices[primitive * 3 + 2] == selectedVertex)
            .ToArray();

        Assert.That(primitiveMask.ToArray(), Is.EqualTo(expectedMask),
            "MeshSelectorJob must filter primitives from the LOD0 index range");
    }

    [Test]
    public void TestMeshLodHandling(
        [Values(IndexFormat.UInt16, IndexFormat.UInt32)] IndexFormat indexFormat
    )
    {
        var mesh = CreateMeshWithLods(indexFormat);
        const int removedVertex = 40;

        var lod0Indices = mesh.GetIndices(0, 0);
        var expectedTriangles = new List<int>();
        for (var index = 0; index < lod0Indices.Length; index += 3)
        {
            if (lod0Indices[index] == removedVertex
                || lod0Indices[index + 1] == removedVertex
                || lod0Indices[index + 2] == removedVertex) continue;

            expectedTriangles.Add(lod0Indices[index]);
            expectedTriangles.Add(lod0Indices[index + 1]);
            expectedTriangles.Add(lod0Indices[index + 2]);
        }

        var newMesh = RemoveVerticesFromMesh.RemoveVertices(null!, mesh, new[]
        {
            (new TargetProp(), (IMeshSelector) new SpecificVertexFilter(new[] { removedVertex }))
        });

        var originalVertexByPosition = mesh.vertices
            .Select((position, index) => (position, index))
            .ToDictionary(pair => pair.position, pair => pair.index);
        var newVertices = newMesh.vertices;
        var actualTriangles = newMesh.GetIndices(0, 0)
            .Select(index => originalVertexByPosition[newVertices[index]])
            .ToArray();

        Assert.That(CanonicalizeTriangles(actualTriangles),
            Is.EquivalentTo(CanonicalizeTriangles(expectedTriangles)),
            "Vertex removal must operate on the LOD0 index range");
        Assert.That(newMesh.lodCount, Is.GreaterThan(1),
            "Mesh LODs must be regenerated after vertex removal");
    }

    // Generating LODs shuffles the primitive order within the LOD0 index buffer, sort into a consistent
    // order so we can test for equivalence.
    private static List<Vector3Int> CanonicalizeTriangles(IReadOnlyList<int> indices)
    {
        var triangles = new List<Vector3Int>(indices.Count / 3);
        for (var index = 0; index < indices.Count; index += 3)
        {
            var a = indices[index];
            var b = indices[index + 1];
            var c = indices[index + 2];

            if (a > b) (a, b) = (b, a);
            if (b > c) (b, c) = (c, b);
            if (a > b) (a, b) = (b, a);

            triangles.Add(new Vector3Int(a, b, c));
        }

        return triangles;
    }

    private static Mesh CreateMeshWithLods(IndexFormat indexFormat)
    {
        const int sideLength = 17;
        var mesh = new Mesh { indexFormat = indexFormat };
        mesh.vertices = Enumerable.Range(0, sideLength * sideLength)
            .Select(index => new Vector3(index % sideLength, index / sideLength, 0))
            .ToArray();

        var triangles = new List<int>();
        for (var y = 0; y < sideLength - 1; y++)
        {
            for (var x = 0; x < sideLength - 1; x++)
            {
                var bottomLeft = y * sideLength + x;
                triangles.AddRange(new[]
                {
                    bottomLeft, bottomLeft + sideLength, bottomLeft + 1,
                    bottomLeft + 1, bottomLeft + sideLength, bottomLeft + sideLength + 1
                });
            }
        }

        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        MeshLodUtility.GenerateMeshLods(mesh, 2);
        Assert.That(mesh.lodCount, Is.GreaterThan(1), "Failed to generate Mesh LOD test data");
        var lod0 = mesh.GetLod(0, 0);
        var submesh = mesh.GetSubMesh(0);
        Assert.That(lod0.indexStart != submesh.indexStart || lod0.indexCount != submesh.indexCount, Is.True,
            "Test mesh must store LOD0 separately from the full submesh index range");
        return mesh;
    }
#endif

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
            (new TargetProp(), (IMeshSelector) new SpecificVertexFilter(new int[] { 6, 7 }))
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

        var newDeltaVertices = new Vector3[newMesh.vertexCount];
        var newDeltaNormals = new Vector3[newMesh.vertexCount];
        var newDeltaTangents = new Vector3[newMesh.vertexCount];
        newMesh.GetBlendShapeFrameVertices(0, 0, newDeltaVertices, newDeltaNormals, newDeltaTangents);

        Assert.That(newDeltaVertices, Is.EqualTo(deltaVertices.Take(6).ToArray()), "Blend shape delta vertices should be preserved");
        Assert.That(newDeltaNormals, Is.EqualTo(deltaNormals.Take(6).ToArray()), "Blend shape delta normals should be preserved");
        Assert.That(newDeltaTangents, Is.EqualTo(deltaTangents.Take(6).ToArray()), "Blend shape delta tangents should be preserved");
    }

    [Test]
    public void EmptyMeshIsClonedWithoutRetainingANonexistentVertex()
    {
        var original = new Mesh { subMeshCount = 1 };

        var result = RemoveVerticesFromMesh.RemoveVertices(null!, original,
            Array.Empty<(TargetProp, IMeshSelector)>());

        Assert.AreNotSame(original, result);
        Assert.AreEqual(0, result.vertexCount);
        Assert.AreEqual(1, result.subMeshCount);
    }

    [Test]
    public void TestHighDimensionalUVPreservation()
    {
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        int vertexCount = 8;
        mesh.vertices = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i, i * 2, i * 3)).ToArray();

        // Set up 3D UV channel (using channel 2)
        var uv3D = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i * 0.1f, i * 0.2f, i * 0.3f)).ToArray();
        mesh.SetUVs(2, uv3D);

        // Set up 4D UV channel (using channel 3)
        var uv4D = Enumerable.Range(0, vertexCount).Select(i => new Vector4(i * 0.1f, i * 0.2f, i * 0.3f, i * 0.4f)).ToArray();
        mesh.SetUVs(3, uv4D);

        mesh.triangles = new int[] { 0, 1, 2, 3, 4, 5, 5, 6, 7 };

        var newMesh = RemoveVerticesFromMesh.RemoveVertices(null!, mesh, new[]
        {
            (new TargetProp(), (IMeshSelector) new SpecificVertexFilter(new int[] { 6, 7 }))
        });

        Assert.AreEqual(6, newMesh.vertexCount, "Should have 6 vertices remaining");

        var originalUV3D = new List<Vector3>();
        mesh.GetUVs(2, originalUV3D);
        var newUV3D = new List<Vector3>();
        newMesh.GetUVs(2, newUV3D);
        Assert.That(newUV3D, Is.EqualTo(originalUV3D.Take(6).ToList()), "3D UV channel should be preserved");

        var originalUV4D = new List<Vector4>();
        mesh.GetUVs(3, originalUV4D);
        var newUV4D = new List<Vector4>();
        newMesh.GetUVs(3, newUV4D);
        Assert.That(newUV4D, Is.EqualTo(originalUV4D.Take(6).ToList()), "4D UV channel should be preserved");
    }

    private class SpecificVertexFilter : IMeshSelector
    {
        private readonly HashSet<int> _toDeleteVertices;

        public SpecificVertexFilter(IEnumerable<int> toDeleteVertices)
        {
            _toDeleteVertices = toDeleteVertices.ToHashSet();
        }

        public bool Equals(IMeshSelector other) => throw new NotImplementedException();

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            return job.MarkPrimitivesFromVertexIndices(
                this,
                () =>
                {
                    var mask = new NativeArray<bool>(job.OriginalMesh.vertexCount, Allocator.TempJob);
                    foreach (var i in _toDeleteVertices)
                        if (i < mask.Length) mask[i] = true;
                    return (mask, default);
                },
                VertexSelectionMode.AnyVertex,
                submesh,
                selectedPrimitives
            );
        }
    }
}
