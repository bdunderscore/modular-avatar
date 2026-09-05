using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class NaNimationFilterTest : TestBase
{
    [Test]
    public void AllPrimitivesSelected_DoesNotDuplicateVertices()
    {
        var mesh = new Mesh
        {
            vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                Vector3.one
            },
            triangles = new[]
            {
                1, 2, 0,
                0, 3, 4
            }
        };
        var originalVertexCount = mesh.vertexCount;

        NaNimationFilter.ComputeNaNPlan(null, ref mesh,
            new List<(int, IMeshSelector)>
            {
                (0, new PrimitiveMaskSelector(true, true))
            });

        Assert.That(mesh.vertexCount, Is.EqualTo(originalVertexCount));
    }

    [Test]
    public void DuplicateVertex_RedirectsAllMatchingReferences()
    {
        var mesh = new Mesh
        {
            vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up
            },
            triangles = new[]
            {
                0, 0, 0,
                0, 1, 2
            }
        };

        NaNimationFilter.ComputeNaNPlan(null, ref mesh,
            new List<(int, IMeshSelector)>
            {
                (0, new PrimitiveMaskSelector(true, false)),
                (1, new PrimitiveMaskSelector(false, true))
            });

        Assert.That(mesh.vertexCount, Is.EqualTo(4));
        Assert.That(mesh.triangles[0], Is.GreaterThanOrEqualTo(3));
        Assert.That(mesh.triangles[1], Is.EqualTo(mesh.triangles[0]));
        Assert.That(mesh.triangles[2], Is.EqualTo(mesh.triangles[0]));
    }

    [Test]
    public void HideMeshSection_ApplyStatic_BatchesMeshMutationUntilContextDispose()
    {
        var renderer = CreateRoot("renderer").AddComponent<SkinnedMeshRenderer>();
        var mesh = new Mesh
        {
            vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                Vector3.one,
                Vector3.back
            },
            triangles = new[] { 0, 1, 2, 3, 4, 5 }
        };
        renderer.sharedMesh = mesh;
        var first = new PrimitiveMaskSelector(true, false);
        var second = new PrimitiveMaskSelector(false, true);
        using (var context = new StaticApplyContext())
        {
            Assert.AreEqual(StaticApplyResult.Applied,
                new HideMeshSection(MeshSectionTarget.ForMask(renderer, first), first).ApplyStatic(context));
            Assert.AreEqual(StaticApplyResult.Applied,
                new HideMeshSection(MeshSectionTarget.ForMask(renderer, second), second).ApplyStatic(context));
            Assert.AreSame(mesh, renderer.sharedMesh, "Static mesh operations must be deferred and batched.");
        }

        Assert.AreEqual(3, renderer.sharedMesh.triangles.Length);
        Assert.IsTrue(renderer.sharedMesh.triangles.All(index => index == renderer.sharedMesh.triangles[0]),
            "Fully removed meshes retain only the required degenerate triangle.");
    }

    private sealed class PrimitiveMaskSelector : IMeshSelector
    {
        private readonly bool[] _primitiveMask;

        public PrimitiveMaskSelector(params bool[] primitiveMask)
        {
            _primitiveMask = primitiveMask;
        }

        public bool Equals(IMeshSelector other) => ReferenceEquals(this, other);

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh,
            NativeSlice<bool> selectedPrimitives)
        {
            for (var i = 0; i < selectedPrimitives.Length; i++)
                selectedPrimitives[i] = _primitiveMask[i];
            return default;
        }
    }
}
