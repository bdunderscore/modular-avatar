using System.Collections.Generic;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
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
            new List<(TargetProp, IMeshSelector)>
            {
                (new TargetProp(), new PrimitiveMaskSelector(true, true))
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
            new List<(TargetProp, IMeshSelector)>
            {
                (new TargetProp(), new PrimitiveMaskSelector(true, false)),
                (new TargetProp(), new PrimitiveMaskSelector(false, true))
            });

        Assert.That(mesh.vertexCount, Is.EqualTo(4));
        Assert.That(mesh.triangles[0], Is.GreaterThanOrEqualTo(3));
        Assert.That(mesh.triangles[1], Is.EqualTo(mesh.triangles[0]));
        Assert.That(mesh.triangles[2], Is.EqualTo(mesh.triangles[0]));
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
