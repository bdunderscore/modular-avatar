#nullable enable

using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByAxisComponent))]
    internal sealed class VertexFilterByAxis : IVertexFilter
    {
        private readonly Vector3 _center;
        private readonly Vector3 _axis;
        private readonly Transform _avatarRoot;
        
        public VertexFilterByAxis(VertexFilterByAxisComponent component, ComputeContext context)
        {
            (_center, _axis) = context.Observe(component, c => (c.Center, c.Axis));
            _avatarRoot = RuntimeUtil.FindAvatarTransformInParents(component.transform);
        }

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByAxis
                   && ((VertexFilterByAxis)other)._axis == _axis
                   && ((VertexFilterByAxis)other)._center == _center;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(typeof(VertexFilterByAxis), _axis, _center);
        }

        public override string ToString()
        {
            return $"VertexFilterByAxis: {_axis} @ {_center}";
        }

        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            var referenceTransform = renderer.transform;

            var rootBoneTransform = renderer.transform;
            Mesh? temporaryMesh = null;
            try
            {
                if (renderer is SkinnedMeshRenderer smr)
                {
                    temporaryMesh = new Mesh();
                    var originalMesh = smr.sharedMesh;
                    try
                    {
                        smr.sharedMesh = mesh;
                        smr.BakeMesh(temporaryMesh, true);
                    }
                    finally
                    {
                        smr.sharedMesh = originalMesh;
                    }

                    mesh = temporaryMesh;
                }

                var meshSpaceCenter = _center;
                var meshSpaceAxis = _axis;

                var originalRenderer = NDMFPreview.GetOriginalObjectForProxy(renderer.gameObject);
                if (originalRenderer != null)
                {
                    // Translate the meshSpaceCenter coordinates from the original renderer to the new
                    // renderer's coordinate space; in preview, these don't match in general.
                    meshSpaceCenter = renderer.transform.InverseTransformPoint(
                        originalRenderer.transform.TransformPoint(meshSpaceCenter)
                    );
                    meshSpaceAxis = renderer.transform.InverseTransformDirection(
                        originalRenderer.transform.TransformDirection(meshSpaceAxis)
                    );
                }
                
                var vertices = mesh.vertices;

                if (vertices.Length != filtered.Length)
                {
                    throw new ArgumentException("Mesh vertex count does not match filtered array length.");
                }

                for (var i = 0; i < vertices.Length; i++)
                {
                    if (Vector3.Dot(meshSpaceAxis, vertices[i] - meshSpaceCenter) > 0.0f)
                    {
                        filtered[i] = true;
                    }
                }
            }
            finally
            {
                if (temporaryMesh != null)
                {
                    Object.DestroyImmediate(temporaryMesh);
                }
            }
        }
    }
}