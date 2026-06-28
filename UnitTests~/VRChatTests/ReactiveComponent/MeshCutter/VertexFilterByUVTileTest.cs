using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using Unity.Collections;
using Unity.Jobs;
using nadena.dev.ndmf.preview;
using System.Linq;

public class VertexFilterByUVTileTest : TestBase
{
    private GameObject avatarRoot;
    private MeshRenderer meshRenderer;
    private Mesh testMesh;

    [SetUp]
    public override void Setup()
    {
        base.Setup();

        testMesh = CreateUVTestMesh();

        avatarRoot = CreateRoot("UVTileFilterTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;

        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }

    // 9 vertices with UVs (TexCoord0):
    // v0: (0.1, 0.1)  - inside [0,0.5]x[0,0.5]
    // v1: (0.2, 0.2)  - inside
    // v2: (0.3, 0.3)  - inside
    // v3: (0.1, 0.7)  - U inside, V outside (>0.5)
    // v4: (0.7, 0.1)  - U outside, V inside
    // v5: (0.8, 0.8)  - both outside
    // v6: (-0.1, 0.1) - U outside (<0)
    // v7: (0.4, 0.4)  - inside
    // v8: (0.5, 0.5)  - on boundary (inside)

    // 5 triangles:
    // t0: 0,1,2 - all inside
    // t1: 3,4,5 - all outside
    // t2: 0,1,6 - 2 inside, 1 outside; centroid=(0.067,0.133) inside
    // t3: 3,5,8 - all outside; centroid=(0.467,0.667) outside
    // t4: 0,7,8 - all inside

    private Mesh CreateUVTestMesh()
    {
        var vertices = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), // v0-v2
            new Vector3(1, 1, 0), new Vector3(2, 0, 0), new Vector3(2, 1, 0), // v3-v5
            new Vector3(-1, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 1, 0), // v6-v8
        };
        var triangles = new int[]
        {
            0, 1, 2, // t0
            3, 4, 5, // t1
            0, 1, 6, // t2
            3, 5, 8, // t3
            0, 7, 8, // t4
        };
        var uv = new Vector2[]
        {
            new(0.1f, 0.1f), new(0.2f, 0.2f), new(0.3f, 0.3f),
            new(0.1f, 0.7f), new(0.7f, 0.1f), new(0.8f, 0.8f),
            new(-0.1f, 0.1f), new(0.4f, 0.4f), new(0.5f, 0.5f),
        };

        var mesh = TrackObject(new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            uv = uv,
        });
        mesh.RecalculateBounds();
        return mesh;
    }

    private VertexFilterByUVTileComponent MakeComponent(
        bool useUMin, float uMin, bool useUMax, float uMax,
        bool useVMin, float vMin, bool useVMax, float vMax,
        bool invert = false,
        VertexSelectionMode mode = VertexSelectionMode.AnyVertex,
        int uvChannel = 0)
    {
        var component = avatarRoot.AddComponent<VertexFilterByUVTileComponent>();
        component.UVChannel = uvChannel;
        component.UseUMin = useUMin;
        component.UMin = uMin;
        component.UseUMax = useUMax;
        component.UMax = uMax;
        component.UseVMin = useVMin;
        component.VMin = vMin;
        component.UseVMax = useVMax;
        component.VMax = vMax;
        component.Invert = invert;
        component.SelectionMode = mode;
        return component;
    }

    [Test]
    public void TestNoBoundsEnabled()
    {
        var component = MakeComponent(false, 0, false, 1, false, 0, false, 1);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // All bounds disabled → all primitives selected
        Assert.IsTrue(filtered.All(x => x), "No bounds enabled should select all primitives");
    }

    [Test]
    public void TestSingleBoundUMin()
    {
        var component = MakeComponent(true, 0.5f, false, 1, false, 0, false, 1);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // U >= 0.5 required
        // t0: all U in [0.1,0.3] → false
        // t1: all U in [0.1,0.8] → v3=0.1 (fails), v4=0.7(pass), v5=0.8(pass) → AnyVertex=true, AllVertices=false
        // Since default mode is AnyVertex, t1: v4=0.7 >= 0.5, v5=0.8 >= 0.5 → selected
        // t2: U in [0.1,0.2,-0.1] → only 0.1,0.2 < 0.5, -0.1 < 0.5 → none selected → false
        // t3: U in [0.1,0.8,0.5] → v5=0.8 >= 0.5, v8=0.5 >= 0.5 → selected
        // t4: U in [0.1,0.4,0.5] → v8=0.5 >= 0.5 → selected
        CollectionAssert.AreEqual(new[] { false, true, false, true, true }, filtered);
    }

    [Test]
    public void TestSingleBoundUMinInclusive()
    {
        // MakeComponent defaults all inclusive to false, so set manually
        var component = avatarRoot.AddComponent<VertexFilterByUVTileComponent>();
        component.UseUMin = true;
        component.UMinInclusive = true;
        component.UMin = 0.5f;
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // Inclusive (<=): uv >= 0.5 required (boundary included by inclusive)
        // t0: U in [0.1,0.3] all < 0.5 → false
        // t1: U in [0.1,0.7,0.8] → v4=0.7, v5=0.8 → true
        // t2: U in [0.1,0.2,-0.1] all < 0.5 → false
        // t3: U in [0.1,0.8,0.5] → v5=0.8, v8=0.5 (boundary now kept) → true
        // t4: U in [0.1,0.4,0.5] → v8=0.5 (boundary now kept) → true
        CollectionAssert.AreEqual(new[] { false, true, false, true, true }, filtered);
    }

    [Test]
    public void TestSingleBoundUMaxInclusive()
    {
        var component = avatarRoot.AddComponent<VertexFilterByUVTileComponent>();
        component.UseUMax = true;
        component.UMaxInclusive = true;
        component.UMax = 0.5f;
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);
        // Inclusive (<=): uv <= 0.5 required (boundary included by inclusive)

        // t0: U in [0.1,0.3] all < 0.5 → true
        // t1: U in [0.1,0.7,0.8] → v3=0.1 < 0.5 → true
        // t2: U in [0.1,0.2,-0.1] all < 0.5 → true
        // t3: U in [0.1,0.8,0.5] → v3=0.1 < 0.5 → true (v8=0.5 excluded by inclusive)
        // t4: U in [0.1,0.4,0.5] → v0=0.1 < 0.5, v7=0.4 < 0.5 → true (v8=0.5 excluded)
        CollectionAssert.AreEqual(new[] { true, true, true, true, true }, filtered);
    }

    [Test]
    public void TestSingleBoundUMax()
    {
        var component = MakeComponent(false, 0, true, 0.5f, false, 0, false, 1);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // U <= 0.5 required
        // t0: all U in [0.1,0.3] <= 0.5 → true
        // t1: U in [0.1,0.7,0.8] → v4=0.7 > 0.5, v5=0.8 > 0.5 → selected via v3=0.1
        // t2: U in [0.1,0.2,-0.1] → all <= 0.5 → true
        // t3: U in [0.1,0.8,0.5] → v5=0.8 > 0.5, v8=0.5 <= 0.5 → selected via v3=0.1, v8=0.5
        // t4: U in [0.1,0.4,0.5] → all <= 0.5 → true
        CollectionAssert.AreEqual(new[] { true, true, true, true, true }, filtered);
    }

    [Test]
    public void TestBothUBounds()
    {
        var component = MakeComponent(true, 0.2f, true, 0.8f, false, 0, false, 1);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // U in [0.2, 0.8]
        // t0: U in [0.1,0.2,0.3] → v0=0.1 < 0.2 (fails), v1=0.2 (pass), v2=0.3 (pass) → AnyVertex=true
        // t1: U in [0.1,0.7,0.8] → v3=0.1 < 0.2 (fails), v4=0.7 (pass), v5=0.8 (pass) → AnyVertex=true
        // t2: U in [0.1,0.2,-0.1] → v0=0.1<0.2, v6=-0.1<0.2 (fails), v1=0.2 (pass) → AnyVertex=true
        // t3: U in [0.1,0.8,0.5] → v3=0.1<0.2, v5=0.8(pass), v8=0.5(pass) → AnyVertex=true
        // t4: U in [0.1,0.4,0.5] → v0=0.1<0.2, v7=0.4(pass), v8=0.5(pass) → AnyVertex=true
        CollectionAssert.AreEqual(new[] { true, true, true, true, true }, filtered);
    }

    [Test]
    public void TestBothAxes()
    {
        var component = MakeComponent(true, 0f, true, 0.5f, true, 0f, true, 0.5f);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // U in [0,0.5] AND V in [0,0.5]
        // t0: all inside → true
        // t1: v3 V=0.7 > 0.5, v4 U=0.7 > 0.5... wait, let me recheck.
        // v3=(0.1,0.7) → U=0.1 ok, V=0.7 > 0.5 → outside
        // v4=(0.7,0.1) → U=0.7 > 0.5 → outside
        // v5=(0.8,0.8) → both outside
        // All outside → false
        // t2: v6=(-0.1,0.1) → U=-0.1 < 0 → outside. v0,v1 inside.
        // 2 inside, 1 outside → AnyVertex=true
        // t3: v3=(0.1,0.7) V outside, v5=(0.8,0.8) both outside, v8=(0.5,0.5) inside
        // 1 inside → AnyVertex=true
        // t4: v0=(0.1,0.1) inside, v7=(0.4,0.4) inside, v8=(0.5,0.5) inside → all inside → true
        CollectionAssert.AreEqual(new[] { true, false, true, true, true }, filtered);
    }

    [Test]
    public void TestBothAxesAllVertices()
    {
        var component = MakeComponent(true, 0f, true, 0.5f, true, 0f, true, 0.5f,
            mode: VertexSelectionMode.AllVertices);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // U in [0,0.5] AND V in [0,0.5], AllVertices
        // t0: all 3 inside → true
        // t1: all outside → false
        // t2: 1 outside → false
        // t3: 2 outside → false
        // t4: all inside → true
        CollectionAssert.AreEqual(new[] { true, false, false, false, true }, filtered);
    }

    [Test]
    public void TestInvert()
    {
        var component = MakeComponent(true, 0f, true, 0.5f, true, 0f, true, 0.5f,
            invert: true);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // Invert: outside [0,0.5]x[0,0.5]
        // t0: all inside → false
        // t1: all outside → true
        // t2: 1 outside (v6) → AnyVertex=true → invert → true
        // t3: v3,v5 outside, v8 inside → AnyVertex=true → invert → true
        // t4: all inside → false
        CollectionAssert.AreEqual(new[] { false, true, true, true, false }, filtered);
    }
    [Test]
    public void TestAnyVertexMode()
    {
        // Same bounds as TestBothAxes — default AnyVertex already tested above
        var component = MakeComponent(true, 0f, true, 0.5f, true, 0f, true, 0.5f,
            mode: VertexSelectionMode.AnyVertex);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // Same as TestBothAxes: t2 has 1 outside but 2 inside → selected
        CollectionAssert.AreEqual(new[] { true, false, true, true, true }, filtered);
    }

    [Test]
    public void TestAllVerticesMode()
    {
        // Already tested via TestBothAxesAllVertices — explicit repeat for clarity
        var component = MakeComponent(true, 0f, true, 0.5f, true, 0f, true, 0.5f,
            mode: VertexSelectionMode.AllVertices);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, false, false, false, true }, filtered);
    }

    [Test]
    public void TestCentroidMode()
    {
        var component = MakeComponent(true, 0f, true, 0.5f, true, 0f, true, 0.5f,
            mode: VertexSelectionMode.Centroid);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        // t0 centroid: (0.2,0.2) inside
        // t1 centroid: (0.533,0.533) outside
        // t2 centroid: (0.067,0.133) inside
        // t3 centroid: (0.467,0.667) outside
        // t4 centroid: (0.333,0.333) inside
        CollectionAssert.AreEqual(new[] { true, false, true, false, true }, filtered);
    }

    [Test]
    public void TestUVChannel()
    {
        // Mesh with two UV channels: TexCoord0 has a known layout,
        // TexCoord1 (uv2) has a different layout.
        // We use a separate mesh for this test to avoid polluting the main one.
        var mesh = CreateDualUVTestMesh();

        var go = CreateRoot("UVChannelTest");
        var r = go.AddComponent<MeshRenderer>();
        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        // Filter on UV channel 0 (default): U in [0,0.5], V in [0,0.5]
        var component0 = go.AddComponent<VertexFilterByUVTileComponent>();
        component0.UseUMin = true;
        component0.UMin = 0f;
        component0.UseUMax = true;
        component0.UMax = 0.5f;
        component0.UseVMin = true;
        component0.VMin = 0f;
        component0.UseVMax = true;
        component0.VMax = 0.5f;
        component0.UVChannel = 0;
        var filter0 = new VertexFilterByUVTile(component0, ComputeContext.NullContext);
        var filtered0 = RunFilterPrimitives(filter0, r, mesh);

        // Channel 1 has U around [0.6,0.9], V around [0.6,0.9]
        // So U in [0,0.5] on channel 1 selects nothing
        var component1 = go.AddComponent<VertexFilterByUVTileComponent>();
        component1.UseUMin = true;
        component1.UMin = 0f;
        component1.UseUMax = true;
        component1.UMax = 0.5f;
        component1.UseVMin = true;
        component1.VMin = 0f;
        component1.UseVMax = true;
        component1.VMax = 0.5f;
        component1.UVChannel = 1;
        var filter1 = new VertexFilterByUVTile(component1, ComputeContext.NullContext);
        var filtered1 = RunFilterPrimitives(filter1, r, mesh);

        // Channel 0 should have normal behavior (same as TestCentroidMode)
        // Channel 1 UVs are outside [0,0.5]x[0,0.5], so nothing selected
        CollectionAssert.AreEqual(new[] { true, false, true, false, true }, filtered0,
            "Channel 0 should use TexCoord0 UVs");
        Assert.IsTrue(filtered1.All(x => !x), "Channel 1 UVs should be outside the [0,0.5] rectangle");
    }

    private Mesh CreateDualUVTestMesh()
    {
        // Same vertex layout but different UVs on channel 1
        var mesh = CreateUVTestMesh();
        var uv2 = new Vector2[]
        {
            new(0.6f, 0.6f), new(0.7f, 0.7f), new(0.8f, 0.8f),
            new(0.6f, 0.9f), new(0.9f, 0.6f), new(0.9f, 0.9f),
            new(0.5f, 0.6f), new(0.7f, 0.7f), new(0.8f, 0.6f),
        };
        mesh.SetUVs(1, uv2);
        return mesh;
    }
    [Test]
    public void TestPreFilteredPrimitivesRemainUnfiltered()
    {
        // Bounds that select nothing: all UVs have U<2, so U in [3,4] selects none
        var component = MakeComponent(true, 3f, true, 4f, true, -1f, true, 2f);
        var filter = new VertexFilterByUVTile(component, new ComputeContext("test"));

        using var selectorJob = new MeshSelectorJob(meshRenderer, testMesh);
        var desc = selectorJob.MeshData.GetSubMesh(0);
        int primCount = desc.indexCount / 3;
        var primMask = new NativeArray<bool>(primCount, Allocator.TempJob);
        try
        {
            for (int i = 0; i < primCount; i++) primMask[i] = (i % 2 == 0); // alternating pre-mark
            var expected = primMask.ToArray();
            filter.MarkFilteredPrimitives(selectorJob, 0, primMask).Complete();

            for (int i = 0; i < primCount; i++)
                Assert.AreEqual(expected[i], primMask[i], $"Primitive {i} state should be unchanged");
        }
        finally
        {
            primMask.Dispose();
        }
    }

    [Test]
    public void TestEqualsAndHashCode()
    {
        var component1 = MakeComponent(true, 0.1f, true, 0.9f, true, 0.2f, true, 0.8f,
            invert: false, mode: VertexSelectionMode.AllVertices, uvChannel: 1);
        var component2 = MakeComponent(true, 0.1f, true, 0.9f, true, 0.2f, true, 0.8f,
            invert: false, mode: VertexSelectionMode.AllVertices, uvChannel: 1);

        var filter1 = new VertexFilterByUVTile(component1, ComputeContext.NullContext);
        var filter2 = new VertexFilterByUVTile(component2, ComputeContext.NullContext);

        Assert.IsTrue(filter1.Equals(filter2), "Identical filters should be equal");
        Assert.AreEqual(filter1.GetHashCode(), filter2.GetHashCode(),
            "Identical filters should have equal hash codes");

        // Different UVChannel
        component2.UVChannel = 2;
        var filter3 = new VertexFilterByUVTile(component2, ComputeContext.NullContext);
        Assert.IsFalse(filter1.Equals(filter3), "Different UVChannel should make filters unequal");
        Assert.AreNotEqual(filter1.GetHashCode(), filter3.GetHashCode(),
            "Different UVChannel should produce different hash codes");
    }

    [Test]
    public void TestToString()
    {
        var component = MakeComponent(true, 0.1f, true, 0.9f, false, 0, false, 1);
        var filter = new VertexFilterByUVTile(component, ComputeContext.NullContext);
        var str = filter.ToString();

        Assert.IsNotNull(str);
        Assert.IsTrue(str.Contains("UVTile"), "ToString should contain 'UVTile'");
        Assert.IsTrue(str.Contains("0.9"), "ToString should contain bound values");
    }
}