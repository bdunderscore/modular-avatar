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
    public void VertexFilterByMaskClampUVTest()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = renderer.sharedMesh;
        var mask = LoadAsset<Texture2D>("DeletionTest/MaskTexture_Clamp.png");

        var inRangeUvs = mesh.uv.Count(new Rect(0, 0, 1, 1).Contains);
        var outRangeUvs = mesh.vertexCount - inRangeUvs;

        Assert.AreEqual(12, inRangeUvs);
        Assert.AreEqual(12, outRangeUvs);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack);
        var filtered = new bool[mesh.vertexCount];
        Array.Fill(filtered, true);
        filter.MarkFilteredVertices(renderer, mesh, filtered);

        Assert.AreEqual(inRangeUvs, filtered.Count(x => x));
        Assert.AreEqual(outRangeUvs, filtered.Count(x => !x));
    }

    [Test]
    public void VertexFilterByMaskRepeatUVTest()
    {
        var root = CreatePrefab("DeletionTest/DeletionTest.prefab");
        var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = renderer.sharedMesh;
        var mask = LoadAsset<Texture2D>("DeletionTest/MaskTexture_Repeat.png");

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
}

#endif
