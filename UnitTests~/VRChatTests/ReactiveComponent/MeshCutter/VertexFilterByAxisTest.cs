using modular_avatar_tests;
using UnityEngine;
using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Unity.Collections;
using Object = UnityEngine.Object;

public class VertexFilterByAxisTest : TestBase
{
    private GameObject avatarRoot;
    private MeshRenderer meshRenderer;
    private Mesh testMesh;

    [SetUp]
    public override void Setup()
    {
        base.Setup();

        testMesh = CreateAxisTestMesh();

        avatarRoot = CreateRoot("AxisFilterTestObject");
        meshRenderer = avatarRoot.AddComponent<MeshRenderer>();
        var meshFilter = avatarRoot.AddComponent<MeshFilter>();
        meshFilter.mesh = testMesh;

        // Ensure we start with a consistent position and scale.
        avatarRoot.transform.position = Vector3.zero;
        avatarRoot.transform.rotation = Quaternion.identity;
        avatarRoot.transform.localScale = Vector3.one;
    }

    // Tests for vertex selection with Renderer reference frame
    [Test]
    public void TestVertexSelectionRenderer()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up; // Y-axis up

        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, false, false, true, false }, filtered);
    }

    [Test]
    public void TestVertexSelectionDifferentAxis()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.down; // Negative Y axis

        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { false, true, false, true, true }, filtered);
    }

    [Test]
    public void TestVertexSelectionOffsetCenter()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = new Vector3(0, -0.5f, 0); // Offset center
        component.Axis = Vector3.up;

        var filter = new VertexFilterByAxis(component, new ComputeContext("test"));
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, false, true, true, true }, filtered);
    }

    [Test]
    public void TestPreviewProxyUsesOriginalRendererCoordinateFrame()
    {
        var proxyObject = CreateChild(avatarRoot, "AxisFilterPreviewProxy");
        proxyObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        proxyObject.transform.localScale = Vector3.one;
        var proxyRenderer = proxyObject.AddComponent<MeshRenderer>();

        avatarRoot.transform.position = new Vector3(2, 3, 4);

        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;

        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);

        // Preview proxies are mapped back to their original renderer. When both transforms are
        // identical, the proxy-to-original conversion must be identity even away from world zero.
        using var selectorJob = new MeshSelectorJob(proxyRenderer, testMesh, meshRenderer.gameObject);
        var desc = selectorJob.MeshData.GetSubMesh(0);
        using var primitiveMask = new NativeArray<bool>(desc.indexCount / 3, Allocator.TempJob);

        filter.MarkFilteredPrimitives(selectorJob, 0, primitiveMask).Complete();

        CollectionAssert.AreEqual(new[] { true, false, false, true, false }, primitiveMask.ToArray());
    }

    [Test]
    public void TestAllVerticesSelectionMode()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;
        component.SelectionMode = VertexSelectionMode.AllVertices;

        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, false, false, false, false }, filtered);
    }

    [Test]
    public void TestCentroidSelectionMode()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;
        component.SelectionMode = VertexSelectionMode.Centroid;

        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);
        var filtered = RunFilterPrimitives(filter, meshRenderer, testMesh);

        CollectionAssert.AreEqual(new[] { true, false, false, false, false }, filtered);
    }

    [Test]
    public void TestPreFilteredPrimitivesRemainFiltered()
    {
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;
        var filter = new VertexFilterByAxis(component, new ComputeContext("test"));

        using var selectorJob = new MeshSelectorJob(meshRenderer, testMesh);
        var desc = selectorJob.MeshData.GetSubMesh(0);
        int primCount = desc.indexCount / 3;
        var primMask = new NativeArray<bool>(primCount, Allocator.TempJob);
        try
        {
            for (int i = 0; i < primCount; i++) primMask[i] = true; // pre-mark all
            filter.MarkFilteredPrimitives(selectorJob, 0, primMask).Complete();

            // MarkFilteredPrimitives only sets true, never clears; all pre-marked must remain.
            for (int i = 0; i < primCount; i++)
                Assert.IsTrue(primMask[i], $"Pre-marked primitive {i} should remain marked");
        }
        finally
        {
            primMask.Dispose();
        }
    }

    [Test]
    public void TestUnfilteredPrimitivesRemainUnfiltered()
    {
        // Center at Y=4: no vertices are above Y=4, so the filter marks nothing new.
        var component = avatarRoot.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.up * 4;
        component.Axis = Vector3.up;
        var filter = new VertexFilterByAxis(component, new ComputeContext("test"));

        using var selectorJob = new MeshSelectorJob(meshRenderer, testMesh);
        var desc = selectorJob.MeshData.GetSubMesh(0);
        int primCount = desc.indexCount / 3;
        var primMask = new NativeArray<bool>(primCount, Allocator.TempJob);
        try
        {
            for (int i = 0; i < primCount; i++) primMask[i] = (i % 2 == 0); // alternating pre-mark
            var expected = primMask.ToArray();
            filter.MarkFilteredPrimitives(selectorJob, 0, primMask).Complete();

            // Filter marked nothing; state should be identical to what was pre-populated.
            for (int i = 0; i < primCount; i++)
                Assert.AreEqual(expected[i], primMask[i], $"Primitive {i} state should be unchanged");
        }
        finally
        {
            primMask.Dispose();
        }
    }

    [Test]
    public void TestSkinnedMeshBlendShapeIsBakedBeforeAxisSelection()
    {
        var skinnedObject = CreateChild(avatarRoot, "SkinnedAxisFilter");
        var smr = skinnedObject.AddComponent<SkinnedMeshRenderer>();
        var mesh = CreateSkinnedBlendShapeAxisTestMesh();
        smr.sharedMesh = mesh;
        smr.rootBone = skinnedObject.transform;
        smr.bones = new[] { skinnedObject.transform };

        var component = skinnedObject.AddComponent<VertexFilterByAxisComponent>();
        component.Center = Vector3.zero;
        component.Axis = Vector3.up;

        var filter = new VertexFilterByAxis(component, ComputeContext.NullContext);

        smr.SetBlendShapeWeight(0, 0);
        CollectionAssert.AreEqual(new[] { false }, RunFilterPrimitives(filter, smr, mesh));

        smr.SetBlendShapeWeight(0, 100);
        CollectionAssert.AreEqual(new[] { true }, RunFilterPrimitives(filter, smr, mesh));
    }

    private Mesh CreateAxisTestMesh()
    {
        var vertices = new[]
        {
            new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 1),
            new Vector3(2, -1, 0), new Vector3(3, -1, 0), new Vector3(2, -1, 1),
            new Vector3(4, 0, 0), new Vector3(5, 0, 0), new Vector3(4, 0, 1),
            new Vector3(6, 1, 0), new Vector3(7, -1, 0), new Vector3(6, -1, 1),
            new Vector3(8, -0.25f, 0), new Vector3(9, -0.25f, 0), new Vector3(8, -0.25f, 1)
        };
        var triangles = new int[vertices.Length];
        for (int i = 0; i < triangles.Length; i++) triangles[i] = i;

        var mesh = TrackObject(new Mesh
        {
            vertices = vertices,
            triangles = triangles
        });
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh CreateSkinnedBlendShapeAxisTestMesh()
    {
        var vertices = new[]
        {
            new Vector3(0, -1, 0),
            new Vector3(1, -1, 0),
            new Vector3(0, -1, 1)
        };

        var mesh = TrackObject(new Mesh
        {
            vertices = vertices,
            triangles = new[] { 0, 1, 2 },
            bindposes = new[] { Matrix4x4.identity },
            boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 }
            }
        });

        var deltaVertices = new Vector3[vertices.Length];
        deltaVertices[0] = Vector3.up * 2;
        mesh.AddBlendShapeFrame(
            "RaisesOneVertexAcrossAxis",
            100,
            deltaVertices,
            new Vector3[vertices.Length],
            new Vector3[vertices.Length]
        );
        mesh.RecalculateBounds();
        return mesh;
    }
}
