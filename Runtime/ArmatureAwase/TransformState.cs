#region

using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal struct TransformState
    {
        private const float POS_EPSILON = 0.00001f;
        private const float ROT_EPSILON = 0.00001f;
        private const float SCALE_EPSILON = 0.00001f;

        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        // Read on FromTransform, not written back in ToTransform
        public Matrix4x4 localToWorldMatrix;

        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;
        
        internal static TransformState FromTransform(Transform mergeBone)
        {
            return new TransformState
            {
                localPosition = mergeBone.localPosition,
                localRotation = mergeBone.localRotation,
                localScale = mergeBone.localScale,
                localToWorldMatrix = mergeBone.localToWorldMatrix,
            };
        }

        internal void ToTransform(Transform bone)
        {
#if UNITY_EDITOR
            Undo.RecordObject(bone, Undo.GetCurrentGroupName());
#endif
            bone.localPosition = localPosition;
            bone.localRotation = localRotation;
            bone.localScale = localScale;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Differs(TransformState self, TransformState other)
        {
            var deltaMergePos = (self.localPosition - other.localPosition).sqrMagnitude;
            var deltaMergeRot = self.localRotation * Quaternion.Inverse(other.localRotation);
            var deltaMergeScale = (self.localScale - other.localScale).sqrMagnitude;

            return (deltaMergePos > POS_EPSILON
                    || Quaternion.Angle(deltaMergeRot, Quaternion.identity) > ROT_EPSILON
                    || deltaMergeScale > SCALE_EPSILON);
        }
    }
}