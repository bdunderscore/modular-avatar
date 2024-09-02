using System.Linq;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.plugin
{
    // workaround problem with avatar matching
    // https://github.com/bdunderscore/modular-avatar/issues/430
    internal class RebindHumanoidAvatar
    {
        private readonly ndmf.BuildContext _buildContext;

        public RebindHumanoidAvatar(ndmf.BuildContext context)
        {
            _buildContext = context;
        }

        public void Process()
        {
            var avatarAnimator = _buildContext.AvatarRootObject.GetComponent<Animator>();
            if (avatarAnimator == null || avatarAnimator.avatar == null) return;

            var localTransformValues = _buildContext.AvatarRootObject
                .GetComponentsInChildren<Transform>(true)
                .ToDictionary((t) => t, LocalTransformValue.FromTransform);

            var boundAvatar = avatarAnimator.avatar;
            avatarAnimator.avatar = null;
            // ReSharper disable once Unity.InefficientPropertyAccess
            avatarAnimator.avatar = boundAvatar;

            // resetting avatar also resets local transform value from avatar asset
            // needs to restore them manually from pre-cache
            // https://github.com/bdunderscore/modular-avatar/issues/1036
            foreach (var (transform, preserved) in localTransformValues)
            {
                preserved.RestoreToTransform(transform);
            }
        }

        struct LocalTransformValue
        {
            Vector3 Position;
            Quaternion Rotation;
            Vector3 Scale;

            internal static LocalTransformValue FromTransform(Transform t)
            {
                return new LocalTransformValue
                {
                    Position = t.localPosition,
                    Rotation = t.localRotation,
                    Scale = t.localScale
                };
            }

            internal readonly void RestoreToTransform(Transform t)
            {
                t.SetLocalPositionAndRotation(Position, Rotation);
                t.localScale = Scale;
            }
        }
    }
}