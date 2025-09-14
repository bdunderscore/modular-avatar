#nullable enable
using UnityEngine;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal struct Pose
    {
        public static readonly Pose Zero = new()
        {
            position = Vector3.zero,
            rotation = Quaternion.identity,
            scale = Vector3.one
        };

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public static Pose FromTransform(Transform t)
        {
            return new Pose
            {
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            };
        }

        public static bool Approximately(Pose a, Pose b)
        {
            return Vector3.SqrMagnitude(a.position - b.position) < 0.0001f
                   && Quaternion.Angle(a.rotation, b.rotation) < 0.1f
                   && Vector3.SqrMagnitude(a.scale - b.scale) < 0.0001f;
        }

        public Pose DeltaTo(Pose b)
        {
            return new Pose
            {
                position = b.position - position,
                rotation = b.rotation * Quaternion.Inverse(rotation),
                scale = new Vector3(b.scale.x / scale.x, b.scale.y / scale.y, b.scale.z / scale.z)
            };
        }

        public void Apply(Pose delta)
        {
            position += delta.position;
            rotation = delta.rotation * rotation;
            scale = new Vector3(scale.x * delta.scale.x, scale.y * delta.scale.y, scale.z * delta.scale.z);
        }

        public void ToTransform(Transform t)
        {
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
        }
    }
}