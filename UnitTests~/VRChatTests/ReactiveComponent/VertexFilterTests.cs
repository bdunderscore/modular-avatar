#if MA_VRCSDK3_AVATARS

using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class VertexFilterTests : TestBase
{
    [Test]
    public void VertexFilterByMaskClampUVTest(
        [Values(true, false)]
        bool readable
    )
    {
        var renderer = CreateRoot("MaskFilterClampTestObject").AddComponent<MeshRenderer>();
        var mesh = CreateUvTriangleMesh(
            UvPrim(new Vector2(0.25f, 0.25f)),
            UvPrim(new Vector2(0.75f, 0.75f)),
            new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Vector2(0.75f, 0.75f) },
            UvPrim(new Vector2(-0.25f, 0.25f)),
            UvPrim(new Vector2(1.25f, 0.75f))
        );
        var mask = CreateMaskTexture(readable, TextureWrapMode.Clamp);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack);
        var filtered = RunFilterPrimitives(filter, renderer, mesh);

        CollectionAssert.AreEqual(new[] { true, false, true, true, false }, filtered);
    }

    [Test]
    public void VertexFilterByMaskRepeatUVTest(
        [Values(true, false)]
        bool readable
    )
    {
        var renderer = CreateRoot("MaskFilterRepeatTestObject").AddComponent<MeshRenderer>();
        var mesh = CreateUvTriangleMesh(
            UvPrim(new Vector2(0.25f, 0.25f)),
            UvPrim(new Vector2(0.75f, 0.75f)),
            UvPrim(new Vector2(1.25f, 0.25f)),
            UvPrim(new Vector2(0.25f, 1.25f)),
            UvPrim(new Vector2(-0.25f, 0.75f))
        );
        var mask = CreateMaskTexture(readable, TextureWrapMode.Repeat);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack);
        var filtered = RunFilterPrimitives(filter, renderer, mesh);

        CollectionAssert.AreEqual(new[] { true, false, true, true, false }, filtered);
    }

    [Test]
    public void VertexFilterByMaskAllVerticesModeTest()
    {
        var renderer = CreateRoot("MaskFilterAllVerticesTestObject").AddComponent<MeshRenderer>();
        var mesh = CreateUvTriangleMesh(
            UvPrim(new Vector2(0.25f, 0.25f)),
            new[] { new Vector2(0.25f, 0.25f), new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f) },
            UvPrim(new Vector2(0.75f, 0.75f))
        );
        var mask = CreateMaskTexture(true, TextureWrapMode.Clamp);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack, VertexSelectionMode.AllVertices);
        var filtered = RunFilterPrimitives(filter, renderer, mesh);

        CollectionAssert.AreEqual(new[] { true, false, false }, filtered);
    }

    [Test]
    public void VertexFilterByMaskCentroidModeTest()
    {
        var renderer = CreateRoot("MaskFilterCentroidTestObject").AddComponent<MeshRenderer>();
        var mesh = CreateUvTriangleMesh(
            UvPrim(new Vector2(0.25f, 0.25f)),
            new[] { new Vector2(0.25f, 0.25f), new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f) },
            UvPrim(new Vector2(0.75f, 0.75f))
        );
        var mask = CreateMaskTexture(true, TextureWrapMode.Clamp);

        var filter = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack, VertexSelectionMode.Centroid);
        var filtered = RunFilterPrimitives(filter, renderer, mesh);

        CollectionAssert.AreEqual(new[] { true, true, false }, filtered);
    }

    [Test]
    public void VertexFilterByMaskOnlyMarksSelectedMaterialSubmesh()
    {
        var renderer = CreateRoot("MaskFilterSubmeshTestObject").AddComponent<MeshRenderer>();
        var mesh = CreateTwoSubmeshMaskMesh();
        var mask = CreateMaskTexture(true, TextureWrapMode.Clamp);
        var filter = new VertexFilterByMask(1, mask, ByMaskMode.DeleteBlack);

        CollectionAssert.AreEqual(new[] { false }, RunFilterPrimitives(filter, renderer, mesh, 0));
        CollectionAssert.AreEqual(new[] { true }, RunFilterPrimitives(filter, renderer, mesh, 1));
    }

    [Test]
    public void VertexFilterByMaskAlternateUVChannelTest()
    {
        var renderer = CreateRoot("MaskFilterUVChannelTestObject").AddComponent<MeshRenderer>();
        var mesh = CreateUvTriangleMesh(
            UvPrim(new Vector2(0.25f, 0.25f)),
            UvPrim(new Vector2(0.25f, 0.25f)),
            UvPrim(new Vector2(0.25f, 0.25f))
        );

        // Channel 1 (TexCoord1) has all-white UVs, opposite of channel 0.
        var uv1 = new Vector2[mesh.vertexCount];
        for (int i = 0; i < uv1.Length; i++) uv1[i] = new Vector2(0.75f, 0.75f);
        mesh.SetUVs(1, uv1);

        var mask = CreateMaskTexture(true, TextureWrapMode.Clamp);

        var filterChannel0 = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack, VertexSelectionMode.AnyVertex, 0);
        var filterChannel1 = new VertexFilterByMask(0, mask, ByMaskMode.DeleteBlack, VertexSelectionMode.AnyVertex, 1);

        CollectionAssert.AreEqual(new[] { true, true, true }, RunFilterPrimitives(filterChannel0, renderer, mesh),
            "Channel 0 should select primitives with black UVs");
        CollectionAssert.AreEqual(new[] { false, false, false }, RunFilterPrimitives(filterChannel1, renderer, mesh),
            "Channel 1 should not select primitives with white UVs");
    }

    [Test]
    public void ANDFilter_DoesNotClearFilteredArray()
    {
        int primCount = 5;
        var dummyRenderer = new GameObject().AddComponent<MeshRenderer>();
        var dummyMesh = new Mesh();
        dummyMesh.vertices = new Vector3[primCount];
        // Degenerate triangles: each primitive maps to exactly one vertex index.
        var tris = new int[primCount * 3];
        for (int i = 0; i < primCount; i++) tris[i * 3] = tris[i * 3 + 1] = tris[i * 3 + 2] = i;
        dummyMesh.triangles = tris;

        // filterA marks primitives 0, 1, 2; filterB marks primitives 1, 2, 3
        var filterA = new MockMeshSelector(new[] { true, true, true, false, false });
        var filterB = new MockMeshSelector(new[] { false, true, true, true, false });
        var andFilter = new ANDFilter(new IMeshSelector[] { filterA, filterB });

        using var selectorJob = new MeshSelectorJob(dummyRenderer, dummyMesh);
        var primMask = new NativeArray<bool>(primCount, Allocator.TempJob);
        try
        {
            primMask[4] = true; // pre-mark primitive 4 (would not be matched by AND)
            andFilter.MarkFilteredPrimitives(selectorJob, 0, primMask).Complete();

            // AND of filterA and filterB: only primitives 1 and 2 should be newly set.
            Assert.IsFalse(primMask[0]);
            Assert.IsTrue(primMask[1]);
            Assert.IsTrue(primMask[2]);
            Assert.IsFalse(primMask[3]);
            Assert.IsTrue(primMask[4]); // pre-marked; must not be cleared
        }
        finally
        {
            primMask.Dispose();
        }
    }

    private class MockMeshSelector : IMeshSelector
    {
        private readonly bool[] _mask;
        public MockMeshSelector(bool[] mask) { _mask = mask; }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            for (int i = 0; i < System.Math.Min(_mask.Length, selectedPrimitives.Length); i++)
                if (_mask[i]) selectedPrimitives[i] = true;
            return default;
        }

        public void Observe(nadena.dev.ndmf.preview.ComputeContext context) { }
        public bool Equals(IMeshSelector other) => false;
    }

    private Mesh CreateUvTriangleMesh(params Vector2[][] primitiveUvs)
    {
        var vertices = new Vector3[primitiveUvs.Length * 3];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[vertices.Length];

        for (int p = 0; p < primitiveUvs.Length; p++)
        {
            var baseIndex = p * 3;
            vertices[baseIndex] = new Vector3(p * 2, 0, 0);
            vertices[baseIndex + 1] = new Vector3(p * 2 + 1, 0, 0);
            vertices[baseIndex + 2] = new Vector3(p * 2, 1, 0);
            triangles[baseIndex] = baseIndex;
            triangles[baseIndex + 1] = baseIndex + 1;
            triangles[baseIndex + 2] = baseIndex + 2;

            for (int i = 0; i < 3; i++) uvs[baseIndex + i] = primitiveUvs[p][i];
        }

        var mesh = TrackObject(new Mesh
        {
            vertices = vertices,
            uv = uvs,
            triangles = triangles
        });
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh CreateTwoSubmeshMaskMesh()
    {
        var mesh = TrackObject(new Mesh
        {
            vertices = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 0, 0),
                new Vector3(2, 1, 0)
            },
            uv = new[]
            {
                new Vector2(0.25f, 0.25f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.25f, 0.25f)
            },
            subMeshCount = 2
        });

        mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
        mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2[] UvPrim(Vector2 uv)
    {
        return new[] { uv, uv, uv };
    }

    private Texture2D CreateMaskTexture(bool readable, TextureWrapMode wrapMode)
    {
        var texture = TrackObject(new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
        {
            wrapMode = wrapMode
        });
        texture.SetPixels32(new[]
        {
            new Color32(0, 0, 0, 255),
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255)
        });
        texture.Apply(false, !readable);
        return texture;
    }
}

#endif
