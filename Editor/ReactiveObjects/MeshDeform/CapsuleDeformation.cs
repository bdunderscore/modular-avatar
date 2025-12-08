using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    [MeshDeformationProvider(typeof(ModularAvatarCapsuleDeform))]
    internal sealed class CapsuleDeformation : IMeshDeformation
    {
        private readonly Transform _transform;
        private readonly float _radius;
        private readonly float _height;
        private readonly bool _disableBackHalf;

        public CapsuleDeformation(
            ComputeContext context,
            ModularAvatarCapsuleDeform deform
        )
        {
            (_radius, _height, _disableBackHalf) =
                context.Observe(deform, d => (d.radius, d.height, d.disableBackHalf));

            _height = Mathf.Max(0, _height - _radius * 2) / 2;

            _transform = deform.transform;
        }

        public void Dispose()
        {
        }

        public void ProcessPoint(ref Vector3 pos, ref Vector3 norm, ref Vector3 tangent)
        {
            var majorAxisDistance = pos.y;

            Vector3 center;
            if (Mathf.Abs(majorAxisDistance) <= _height)
            {
                // We're within the cylindrical portion of the capsule
                center = new Vector3(0, majorAxisDistance, 0);
            }
            else if (Mathf.Abs(majorAxisDistance) >= _height + _radius)
            {
                return;
            }
            else
            {
                // We're in the spherical portion of the capsule
                center = new Vector3(0, _height, 0) * Mathf.Sign(majorAxisDistance);
            }

            var delta = pos - center;
            var distance = delta.magnitude;
            var capsuleNorm = delta.normalized;

            if (distance >= _radius) return;

            var newPos = center + capsuleNorm * _radius;
            var posDelta = pos - newPos;

            if (_disableBackHalf && Vector3.Dot(posDelta, norm) < 0f)
            {
                // Try to push in the opposite direction along the normal vector until we find the other
                // side of the capsule. We do this by first converting to a coordinate system where the
                // major axis and normal vector are basis vectors, then flip the coordinate corresponding
                // to the normal vector. Since the up vector (major axis) has not changed, our position
                // vertically on the capsule is unchanged, and so we should still be on the surface.

                if (Vector3.Dot(norm, Vector3.up) > 0.999f)
                {
                    // In the degenerate case, where the normal is aligned with the major axis, we just push
                    // it back along the major axis.
                    newPos = Vector3.up * (_height + _radius) * -Mathf.Sign(majorAxisDistance);
                }
                else
                {
                    var orthoNormal = (norm - Vector3.Project(norm, Vector3.up)).normalized;
                    var normalMag = Vector3.Dot(newPos - center, orthoNormal);
                    newPos = newPos - orthoNormal * normalMag * 2;
                }
            }

            pos = newPos;

            // todo update normals
        }

        public void RenderGizmo()
        {
            var radialSegs = 18;
            var heightSegs = 18;
            var initialColor = Gizmos.color;

            Gizmos.color = Color.green;

            for (var rs = 0; rs < radialSegs; rs++)
            {
                var rot1 = Quaternion.AngleAxis(rs * 360f / radialSegs, Vector3.up);
                var rot2 = Quaternion.AngleAxis((rs + 1) * 360f / radialSegs, Vector3.up);

                for (var hs = 0; hs < heightSegs; hs++)
                {
                    var vrot1 = Quaternion.AngleAxis(hs * 360f / heightSegs, Vector3.right);
                    var vrot2 = Quaternion.AngleAxis((hs + 1) * 360f / heightSegs, Vector3.right);

                    var p1 = rot1 * vrot1 * Vector3.forward * _radius;
                    var p2 = rot1 * vrot2 * Vector3.forward * _radius;
                    var p3 = rot2 * vrot1 * Vector3.forward * _radius;

                    p1 = AdjustForHeight(p1);
                    p2 = AdjustForHeight(p2);
                    p3 = AdjustForHeight(p3);

                    p1 = _transform.TransformPoint(p1);
                    p2 = _transform.TransformPoint(p2);
                    p3 = _transform.TransformPoint(p3);

                    Gizmos.DrawLine(p1, p2);
                    Gizmos.DrawLine(p1, p3);
                }
            }

            Gizmos.color = initialColor;
        }

        private Vector3 AdjustForHeight(Vector3 p)
        {
            if (Mathf.Abs(p.y) < _radius * 0.01f)
            {
                return p;
            }

            return p + Vector3.up * Mathf.Sign(p.y) * _height;
        }
    }
}