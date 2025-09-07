#if MA_VRCSDK3_AVATARS

using System;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using NUnit.Framework;
using UnityEngine;

public class VertexFilterTests : TestBase
{
    [Test]
    public void VertexFilterByMaskClampUVTest(
        [Values("DeletionTest/MaskTexture_Clamp.png", "DeletionTest/MaskTexture_Clamp_NonReadable.png")]
        string maskPath
    )
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = renderer.sharedMesh;
        var mask = LoadAsset<Texture2D>(maskPath);

        var inRangeUvs = mesh.uv.Count(new Rect(0, 0, 1, 1).Contains);
        var outRangeUvs = mesh.vertexCount - inRangeUvs;

        Assert.AreEqual(12, inRangeUvs);
        Assert.AreEqual(12, outRangeUvs);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack);
        var filtered = new bool[mesh.vertexCount];
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.AreEqual(inRangeUvs, filtered.Count(x => x));
        Assert.AreEqual(outRangeUvs, filtered.Count(x => !x));
    }

    [Test]
    public void VertexFilterByMaskRepeatUVTest(
        [Values("DeletionTest/MaskTexture_Repeat.png", "DeletionTest/MaskTexture_Repeat_NonReadable.png")]
        string maskPath
    )
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = renderer.sharedMesh;
        var mask = LoadAsset<Texture2D>(maskPath);

        var inRangeUvs = mesh.uv.Count(new Rect(0, 0, 1, 1).Contains);
        var outRangeUvs = mesh.vertexCount - inRangeUvs;

        Assert.AreEqual(12, inRangeUvs);
        Assert.AreEqual(12, outRangeUvs);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack);
        var filtered = new bool[mesh.vertexCount];
        Array.Fill(filtered, true);
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.AreEqual(mesh.vertexCount, filtered.Count(x => x));
        Assert.AreEqual(0, filtered.Count(x => !x));
    }

    [Test]
    public void ANDFilter_DoesNotClearFilteredArray()
    {
        // Arrange
        int vertexCount = 5;
        var dummyRenderer = new GameObject().AddComponent<MeshRenderer>();
        var dummyMesh = new Mesh();
        dummyMesh.vertices = new Vector3[vertexCount];

        // First filter: marks vertices 0, 1, 2 as true
        var filterA = new MockVertexFilter(new[] { true, true, true, false, false });
        // Second filter: marks vertices 1, 2, 3 as true
        var filterB = new MockVertexFilter(new[] { false, true, true, true, false });

        var andFilter = new ANDFilter(new IVertexFilter[] { filterA, filterB });

        // Pre-populate filtered array with some true values
        var filtered = new bool[vertexCount];
        filtered[4] = true; // Only index 4 is true initially

        // Act
        andFilter.MarkFilteredVertices(dummyRenderer, dummyMesh, filtered);

        // Assert
        // Only indexes 1 and 2 should be set to true by the AND filter (intersection of filterA and filterB)
        // Index 4 should remain true (was set before), others should be false
        Assert.IsFalse(filtered[0]);
        Assert.IsTrue(filtered[1]);
        Assert.IsTrue(filtered[2]);
        Assert.IsFalse(filtered[3]);
        Assert.IsTrue(filtered[4]);
    }

    // Dummy filter for testing
    private class MockVertexFilter : IVertexFilter
    {
        private readonly bool[] _mask;
        public MockVertexFilter(bool[] mask) { _mask = mask; }
        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            for (int i = 0; i < _mask.Length; i++)
            {
                filtered[i] = filtered[i] || _mask[i];
            }
        }
        public void Observe(nadena.dev.ndmf.preview.ComputeContext context) { }
        public bool Equals(IVertexFilter other) => false;
    }
}

#endif
