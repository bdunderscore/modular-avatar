#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;

public class VertexFilterByBoneTests : TestBase
{
    private SkinnedMeshRenderer CreateTestRenderer(BoneWeight1[] boneWeights, byte[] bonesPerVertex, Transform[] bones)
    {
        var go = new GameObject("TestRenderer");
        var renderer = go.AddComponent<SkinnedMeshRenderer>();
        
        var mesh = new Mesh();
        mesh.vertices = new Vector3[bonesPerVertex.Length];
        
        // Convert to NativeArrays and set bone weights
        var nativeBoneWeights = new NativeArray<BoneWeight1>(boneWeights, Allocator.Temp);
        var nativeBonesPerVertex = new NativeArray<byte>(bonesPerVertex, Allocator.Temp);
        
        try
        {
            mesh.SetBoneWeights(nativeBonesPerVertex, nativeBoneWeights);
        }
        finally
        {
            nativeBoneWeights.Dispose();
            nativeBonesPerVertex.Dispose();
        }
        
        renderer.sharedMesh = mesh;
        renderer.bones = bones;
        
        return renderer;
    }

    [Test]
    public void VertexFilterByBoneBasicTest()
    {
        var bone0 = new GameObject("Bone0").transform;
        var bone1 = new GameObject("Bone1").transform;
        var bones = new Transform[] { bone0, bone1 };

        var boneWeights = new BoneWeight1[]
        {
            new BoneWeight1 { boneIndex = 0, weight = 0.8f },
            new BoneWeight1 { boneIndex = 1, weight = 0.2f },
            new BoneWeight1 { boneIndex = 1, weight = 0.9f },
            new BoneWeight1 { boneIndex = 0, weight = 0.1f }
        };
        var bonesPerVertex = new byte[] { 2, 2 }; // 2 vertices, each with 2 bone weights

        var renderer = CreateTestRenderer(boneWeights, bonesPerVertex, bones);
        var mesh = renderer.sharedMesh;

        // Test with bone0 and threshold 0.5f - should match first vertex (0.8 weight)
        var filter = new VertexFilterByBone(bone0, 0.5f);
        var filtered = new bool[mesh.vertexCount];
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.IsTrue(filtered[0], "First vertex should be filtered (bone0 weight 0.8 > 0.5)");
        Assert.IsFalse(filtered[1], "Second vertex should not be filtered (bone0 weight 0.1 < 0.5)");

        // Test with bone1 and threshold 0.5f - should match second vertex (0.9 weight)
        var filter1 = new VertexFilterByBone(bone1, 0.5f);
        var filtered1 = new bool[mesh.vertexCount];
        filter1.MarkFilteredVertices(renderer, mesh, filtered1);

        Assert.IsFalse(filtered1[0], "First vertex should not be filtered (bone1 weight 0.2 < 0.5)");
        Assert.IsTrue(filtered1[1], "Second vertex should be filtered (bone1 weight 0.9 > 0.5)");
    }

    [Test]
    public void VertexFilterByBoneZeroThresholdTest()
    {
        var bone0 = new GameObject("Bone0").transform;
        var bone1 = new GameObject("Bone1").transform;
        var bones = new Transform[] { bone0, bone1 };

        var boneWeights = new BoneWeight1[]
        {
            // Vertex 0: very small weight for bone0
            new BoneWeight1 { boneIndex = 0, weight = 0.001f },
            new BoneWeight1 { boneIndex = 1, weight = 0.999f },
            // Vertex 1: zero weight for bone0
            new BoneWeight1 { boneIndex = 1, weight = 1.0f },
            // Vertex 2: no weight entry for bone0 at all
            new BoneWeight1 { boneIndex = 1, weight = 1.0f }
        };
        var bonesPerVertex = new byte[] { 2, 1, 1 }; // 3 vertices

        var renderer = CreateTestRenderer(boneWeights, bonesPerVertex, bones);
        var mesh = renderer.sharedMesh;

        // Test with bone0 and threshold exactly 0.0f
        var filter = new VertexFilterByBone(bone0, 0.0f);
        var filtered = new bool[mesh.vertexCount];
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.IsTrue(filtered[0], "Vertex with very small weight (0.001) should be included when threshold is 0");
        Assert.IsFalse(filtered[1], "Vertex with zero weight should not be included");
        Assert.IsFalse(filtered[2], "Vertex with no weight entry for bone should not be included");
    }

    [Test]
    public void VertexFilterByBoneWeightNormalizationTest()
    {
        var bone0 = new GameObject("Bone0").transform;
        var bone1 = new GameObject("Bone1").transform;
        var bones = new Transform[] { bone0, bone1 };

        // Create weights that don't sum to 1.0
        var boneWeights = new BoneWeight1[]
        {
            new BoneWeight1 { boneIndex = 0, weight = 0.4f }, // 0.4 / 0.8 = 0.5 after normalization
            new BoneWeight1 { boneIndex = 1, weight = 0.4f }, // 0.4 / 0.8 = 0.5 after normalization
        };
        var bonesPerVertex = new byte[] { 2 }; // 1 vertex with 2 bone weights

        var renderer = CreateTestRenderer(boneWeights, bonesPerVertex, bones);
        var mesh = renderer.sharedMesh;

        // Test with threshold 0.5f - after normalization, bone0 should have exactly 0.5 weight
        var filter = new VertexFilterByBone(bone0, 0.5f);
        var filtered = new bool[mesh.vertexCount];
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.IsTrue(filtered[0], "Vertex should be filtered when normalized weight (0.5) meets threshold (0.5)");

        // Test with threshold just above 0.5f
        var filter2 = new VertexFilterByBone(bone0, 0.51f);
        var filtered2 = new bool[mesh.vertexCount];
        filter2.MarkFilteredVertices(renderer, mesh, filtered2);

        Assert.IsFalse(filtered2[0], "Vertex should not be filtered when normalized weight (0.5) is below threshold (0.51)");
    }

    [Test]
    public void VertexFilterByBoneWithNullBone()
    {
        var bone0 = new GameObject("Bone0").transform;
        var bones = new Transform[] { bone0 };

        var boneWeights = new BoneWeight1[]
        {
            new BoneWeight1 { boneIndex = 0, weight = 1.0f }
        };
        var bonesPerVertex = new byte[] { 1 };

        var renderer = CreateTestRenderer(boneWeights, bonesPerVertex, bones);
        var mesh = renderer.sharedMesh;

        // Test with null bone - should not filter any vertices
        var filter = new VertexFilterByBone(null, 0.01f);
        var filtered = new bool[mesh.vertexCount];
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        var filteredCount = filtered.Count(x => x);
        Assert.AreEqual(0, filteredCount, "Null bone should not filter any vertices");
    }

    [Test]
    public void VertexFilterByBoneEqualsAndHashCode()
    {
        var bone0 = new GameObject("Bone0").transform;
        var bone1 = new GameObject("Bone1").transform;

        var filter1 = new VertexFilterByBone(bone0, 0.5f);
        var filter2 = new VertexFilterByBone(bone0, 0.5f);
        var filter3 = new VertexFilterByBone(bone0, 0.6f);
        var filter4 = new VertexFilterByBone(bone1, 0.5f);
        
        // Test equality
        Assert.IsTrue(filter1.Equals(filter2), "Filters with same bone and threshold should be equal");
        Assert.IsFalse(filter1.Equals(filter3), "Filters with different thresholds should not be equal");
        Assert.IsFalse(filter1.Equals(filter4), "Filters with different bones should not be equal");
        Assert.IsFalse(filter1.Equals(null), "Filter should not equal null");
        
        // Test hash codes
        Assert.AreEqual(filter1.GetHashCode(), filter2.GetHashCode(), "Equal filters should have same hash code");
    }

    [Test]
    public void VertexFilterByBoneObjectRegistryReplacementTest()
    {
        using var _scope = new ObjectRegistryScope(new ObjectRegistry(null));
        
        var bone0 = new GameObject("Bone0").transform;
        var bone1 = new GameObject("Bone1").transform; // replacement bone
        var bone2 = new GameObject("Bone2").transform; // third bone, no replacement
        var bones = new Transform[] { bone0, bone2 };

        // Register bone1 as replacement for bone0
        ObjectRegistry.RegisterReplacedObject(bone0, bone1);

        var boneWeights = new BoneWeight1[]
        {
            new BoneWeight1 { boneIndex = 0, weight = 0.8f }, // vertex 0, bone0
            new BoneWeight1 { boneIndex = 0, weight = 0.3f }, // vertex 1, bone0
            new BoneWeight1 { boneIndex = 1, weight = 0.7f }, // vertex 1, bone2
            new BoneWeight1 { boneIndex = 1, weight = 0.9f }  // vertex 2, bone2
        };
        var bonesPerVertex = new byte[] { 1, 2, 1 }; // 3 vertices, each with 1 bone weight

        var renderer = CreateTestRenderer(boneWeights, bonesPerVertex, bones);
        var mesh = renderer.sharedMesh;

        // Test with bone1 (replacement) and threshold 0.5f - should match first vertex through bone0
        var filter = new VertexFilterByBone(bone1, 0.5f);
        var filtered = new bool[mesh.vertexCount];
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.IsTrue(filtered[0], "First vertex should be filtered (bone0 weight 0.8 > 0.5, accessed via replacement bone1)");
        Assert.IsFalse(filtered[1], "Second vertex should not be filtered (bone0 weight 0.3 < 0.5)");
        Assert.IsFalse(filtered[2], "Third vertex should not be filtered (bone2 has no replacement and is not bone1)");
    }
}

#endif
