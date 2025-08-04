using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByAxisComponent))]
    internal sealed class VertexFilterByAxis : IVertexFilter
    {
        private static readonly List<Vector3> _dbgVisualizePoints = new();
        private static Vector3? _dbgCenter;
        private static Vector3 _dbgDirection;
        private static Mesh _dbgMesh;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            VertexFilterByAxisComponent.onDrawGizmos = DrawDebugVisualizations;
        }

        internal static void DrawDebugVisualizations()
        {
            if (!_dbgCenter.HasValue) return;

            Gizmos.color = Color.white;

            Gizmos.DrawWireMesh(_dbgMesh, Vector3.zero, Quaternion.identity);

            foreach (var point in _dbgVisualizePoints)
            {
                Gizmos.DrawSphere(point, 0.001f);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_dbgCenter.Value, 0.001f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_dbgCenter.Value, _dbgDirection);
        }

        private readonly Vector3 _center;
        private readonly Vector3 _axis;
        private readonly ByAxisReferenceFrame _referenceFrame;
        private readonly Transform _avatarRoot;
        
        public VertexFilterByAxis(VertexFilterByAxisComponent component, ComputeContext context)
        {
            (_center, _axis, _referenceFrame) = context.Observe(component, c => (c.Center, c.Axis, c.ReferenceFrame));
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
            var meshSpaceAxis = _axis;
            var meshSpaceCenter = _center;

            Transform referenceTransform;
            switch (_referenceFrame)
            {
                case ByAxisReferenceFrame.Renderer:
                case ByAxisReferenceFrame.RootBone: // handled below for SMRs
                    referenceTransform = renderer.transform;
                    break;
                default:
                    referenceTransform = _avatarRoot;
                    break;
            }
            
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
                        smr.BakeMesh(temporaryMesh);
                    }
                    finally
                    {
                        smr.sharedMesh = originalMesh;
                    }

                    mesh = temporaryMesh;

                    if (_referenceFrame == ByAxisReferenceFrame.RootBone)
                    {
                        referenceTransform = smr.rootBone != null ? smr.rootBone : smr.transform;
                    }
                }

                meshSpaceCenter =
                    renderer.transform.InverseTransformPoint(referenceTransform.TransformPoint(meshSpaceCenter));
                meshSpaceAxis =
                    renderer.transform.InverseTransformDirection(referenceTransform.TransformDirection(meshSpaceAxis));
                
                var vertices = mesh.vertices;

                if (vertices.Length != filtered.Length)
                    throw new ArgumentException("Mesh vertex count does not match filtered array length.");

                _dbgVisualizePoints.Clear();
                _dbgCenter = meshSpaceCenter;
                _dbgDirection = meshSpaceAxis.normalized;

                for (var i = 0; i < vertices.Length; i++)
                {
                    if (!filtered[i]) continue;

                    _dbgVisualizePoints.Add(vertices[i]);

                    if (Vector3.Dot(meshSpaceAxis, vertices[i] - meshSpaceCenter) <= 0.0f)
                    {
                        filtered[i] = false;
                    }
                }
            }
            finally
            {
                /*if (temporaryMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporaryMesh);
                }*/
                if (_dbgMesh != null)
                {
                    Object.DestroyImmediate(_dbgMesh);
                }

                _dbgMesh = temporaryMesh;
            }
        }
    }
}