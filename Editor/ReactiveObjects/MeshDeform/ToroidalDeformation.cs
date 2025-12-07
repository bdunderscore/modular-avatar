#nullable enable
using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    /// <summary>
    ///     Implements a toroidal exclusion deformation. Points on the mesh that fall within the torus will
    ///     be pushed out to the surface of the torus along the shortest path (ie - the normal from the torus
    ///     surface).
    /// </summary>
    internal sealed class ToroidalDeformation : IMeshDeformation, IDisposable
    {
        private readonly Vector3 _axis = Vector3.up; // normalized (in reference-local if _reference != null)
        private readonly float _majorRadius; // R
        private readonly float _minorRadius; // r

        // Precomputed orthonormal basis perpendicular to _axis for rendering (in same frame as _axis/_center)
        private readonly Vector3 _u = Vector3.right;
        private readonly Vector3 _v = Vector3.forward;

        // small epsilon
        private const float EPS = 1e-6f;

        // Cached gizmo mesh for RenderGizmo (null when not created)
        private Mesh? _gizmoMesh;

        // Optional reference transform. If non-null, all coordinates passed to ctor (center, axis)
        // are interpreted in this transform's local space. ProcessPoint will operate in that local
        // space by converting incoming world-space points to local, applying the deformation,
        // then converting results back to world.
        private readonly Transform _reference;


        private readonly float _falloffStartAngle;
        private readonly float _falloffEndAngle;

        private readonly bool _disableBackHalf;

        // Changed ctor: optional reference transform
        public ToroidalDeformation(ModularAvatarMeshDeform deform)
        {
            _reference = deform.transform;

            // Inner radius is deform.radius
            // Rinner = Rmajor - Rminor
            // Rminor = Rmajor * aspectRatio
            // Rinner = Rmajor - Rmajor * aspectRatio = RMajor * (1 - aspectRatio)
            // RMajor = Rinner / (1 - aspectRatio)

            _majorRadius = deform.radius / (1 - deform.aspectRatio);
            _minorRadius = _majorRadius * deform.aspectRatio;

            _falloffStartAngle = deform.falloffStartAngle;
            _falloffEndAngle = deform.falloffEndAngle;
            _disableBackHalf = deform.disableBackHalf;
        }

        private float minDist = Mathf.Infinity;
        private Vector3 minDistPoint = Vector3.zero;

        /// <summary>
        ///     Computes the relevant distances for a point
        /// </summary>
        /// <param name="pos">The point to query in the reference space</param>
        /// <param name="radialDirection">The direction from the torus center to the ring point closest to the point</param>
        /// <param name="ringCenter">The ring point closest to the query point</param>
        /// <param name="ringNormal">The direction from the ring point to the query point</param>
        /// <param name="distanceFromRingCenter">The distance from the ring point to the query point</param>
        private void ComputeDistances(
            Vector3 pos,
            out Vector3 radialDirection,
            out Vector3 ringCenter,
            out Vector3 ringNormal,
            out float distanceFromRingCenter
        )
        {
            var majorAxisDistance = Vector3.Dot(pos, _axis);
            radialDirection = (pos - _axis * majorAxisDistance).normalized;
            ringCenter = radialDirection * _majorRadius;
            var ringDelta = pos - ringCenter;
            ringNormal = ringDelta.normalized;
            distanceFromRingCenter = ringDelta.magnitude;
        }

        public void ProcessPoint(ref Vector3 pos, ref Vector3 norm, ref Vector3 tangent)
        {
            ComputeDistances(pos, out var radialDirection, out var ringCenter, out var ringNormal,
                out var distanceFromRingCenter);
            if (distanceFromRingCenter > _minorRadius) return;

            var origPos = pos;

            // We always want to push towards the center of the ring, so if the ring normal points outwards
            // along the radial direction, change it to go inwards instead
            var radialRingDot = Vector3.Dot(radialDirection, ringNormal);

            // Check angle falloff
            var angle = Vector3.Angle(-radialDirection, ringNormal);
            var falloff = Mathf.Lerp(1.0f, 0.0f,
                (angle - _falloffStartAngle) / (_falloffEndAngle - _falloffStartAngle));

            if (falloff <= 0.0f)
            {
                return;
            }

            if (radialRingDot > 0)
            {
                ringNormal -= radialDirection * 2f * radialRingDot;
            }

            // Move the point along the ringNormal until distanceFromRingCenter = _minorRadius
            // (lerp by falloff) 
            var newPos = Vector3.Lerp(pos, ringCenter + ringNormal * _minorRadius, falloff);

            if (_disableBackHalf && Vector3.Dot(newPos - pos, norm) > 0f)
            {
                return;
            }

            pos = newPos;

            var originalNorm = norm;
            // Update the normal to match the torus normal (but take care of directionality)
            if (Vector3.Dot(ringNormal, norm) < 0f)
            {
                norm = -ringNormal;
            }
            else
            {
                norm = ringNormal;
            }

            norm = Vector3.Lerp(originalNorm, norm, falloff).normalized;

            var rot = Quaternion.FromToRotation(originalNorm, norm);
            tangent = rot * tangent;
        }

        // Generate and cache a torus mesh centered at origin using the precomputed basis.
        private void EnsureGizmoMesh(int majorSeg = 36 / 2, int minorSeg = 18 / 2)
        {
            // early out if already created (use nullability instead of a flag)
            if (_gizmoMesh != null) return;

            var mesh = new Mesh();
            mesh.name = "ToroidGizmoMesh";

            var vertsCount = majorSeg * minorSeg;
            var verts = new Vector3[vertsCount];
            var norms = new Vector3[vertsCount];

            var vi = 0;
            for (var i = 0; i < majorSeg; ++i)
            {
                var a = i / (float)majorSeg * Mathf.PI * 2f;
                var dirMajor =
                    Mathf.Cos(a) * _u +
                    Mathf.Sin(a) * _v; // outward direction for this major sample (in same frame as _axis)
                var circleCenter =
                    _majorRadius * dirMajor; // local-space circle center (mesh will be drawn at _center in that frame)

                for (var j = 0; j < minorSeg; ++j)
                {
                    var b = j / (float)minorSeg * Mathf.PI * 2f;
                    var localPos = circleCenter + _minorRadius * (Mathf.Cos(b) * dirMajor + Mathf.Sin(b) * _axis);
                    verts[vi] = localPos;
                    norms[vi] = (localPos - circleCenter).normalized;
                    vi++;
                }
            }

            // Build line-strips:
            // - One closed LineStrip per major ring (minorSeg vertices)
            // - One closed LineStrip per minor ring (majorSeg vertices)
            var ringCount = majorSeg;
            var columnCount = minorSeg;
            mesh.vertices = verts;
            mesh.normals = norms;

            mesh.subMeshCount = ringCount + columnCount;

            // Rings (iterate major segments, each ring loops over minorSeg vertices)
            for (var i = 0; i < majorSeg; ++i)
            {
                var ringIndices = new int[minorSeg + 1]; // +1 to close loop
                for (var j = 0; j < minorSeg; ++j)
                {
                    ringIndices[j] = i * minorSeg + j;
                }

                ringIndices[minorSeg] = i * minorSeg + 0; // close
                mesh.SetIndices(ringIndices, MeshTopology.LineStrip, i);
            }

            // Columns (iterate minor segments, each column loops over majorSeg vertices)
            for (var j = 0; j < minorSeg; ++j)
            {
                var colIndices = new int[majorSeg + 1]; // +1 to close loop
                for (var i = 0; i < majorSeg; ++i)
                {
                    colIndices[i] = i * minorSeg + j;
                }

                colIndices[majorSeg] = 0 * minorSeg + j; // close
                mesh.SetIndices(colIndices, MeshTopology.LineStrip, ringCount + j);
            }

            mesh.RecalculateBounds();

            // cache mesh (use nullability instead of an explicit created flag)
            _gizmoMesh = mesh;
        }

        public void RenderGizmo()
        {
            // Adjust the minor radius slightly so it's visible through the mesh it's manipulating.
            var adjustedMinorRadius = _minorRadius * 0.99f;
            var oldColor = Gizmos.color;
            var baseColor = Color.green;
            var inactiveColor = Color.gray;

            float minorAxisTicks = 18;
            float majorAxisTicks = 18;

            // Render the major radius lines. We need these at each angleInterval angle around the ring.
            // Note that the color will change as we approach the falloff radius
            var degreePerMinor = 360f / minorAxisTicks;
            var priorReferencePoint = Vector3.forward * (_majorRadius - adjustedMinorRadius);
            float priorAlpha = 0;

            for (var tick = 0; tick <= minorAxisTicks; tick++)
            {
                var angle = tick * degreePerMinor - 180;
                var falloffCheckAngle = Mathf.Abs(angle);

                var radianAngle = angle * Mathf.Deg2Rad;

                var falloff = (falloffCheckAngle - _falloffStartAngle) / (_falloffEndAngle - _falloffStartAngle);
                var majorColor = Color.Lerp(baseColor, inactiveColor, falloff);
                majorColor.a = Mathf.Lerp(1f, 0.2f, falloff - 1f);

                var minorColor = Color.gray;

                // Offset of point on inner radius of circle
                var refPoint = Vector3.forward * _majorRadius
                               - Mathf.Cos(radianAngle) * Vector3.forward * adjustedMinorRadius
                               + Mathf.Sin(radianAngle) * Vector3.up * adjustedMinorRadius;

                var priorPoint = _reference.TransformPoint(refPoint);
                var segments = 18;
                var angleIncrement = 360f / segments;
                for (var j = 0; j < segments; j++)
                {
                    var rot = Quaternion.AngleAxis(angleIncrement * j, Vector3.up);
                    var targetPoint = rot * refPoint;

                    var xformPoint = _reference.TransformPoint(targetPoint);

                    Gizmos.color = majorColor;
                    Gizmos.DrawLine(priorPoint, xformPoint);
                    priorPoint = xformPoint;

                    // Draw the rings around the donut (minor radius)
                    // These are drawn at the higher of the two alpha levels
                    minorColor.a = Mathf.Max(majorColor.a, priorAlpha);
                    var from = rot * priorReferencePoint;
                    var to = rot * refPoint;
                    from = _reference.TransformPoint(from);
                    to = _reference.TransformPoint(to);
                    Gizmos.color = minorColor;
                    Gizmos.DrawLine(from, to);
                }

                Gizmos.DrawLine(priorPoint, _reference.TransformPoint(refPoint));

                priorAlpha = majorColor.a;
                priorReferencePoint = refPoint;
            }

            Gizmos.color = oldColor;
        }

        // Dispose implementation to destroy cached mesh when done.
        public void Dispose()
        {
        }
    }
}