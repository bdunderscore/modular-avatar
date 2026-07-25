using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    ///     Matches selected transform properties to another object in the avatar hierarchy.
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Move To")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/move-to?lang=auto")]
    public class ModularAvatarMoveTo : AvatarTagComponent, IHaveObjReferences
    {
        public AvatarObjectReference target = new();
        public bool matchPosition = true;
        public bool matchRotation = true;
        public bool matchScale;

        public override void ResolveReferences()
        {
            target?.Get(this);
        }

        internal void Update()
        {
            if (!RuntimeUtil.isPlaying)
            {
                MatchTarget();
            }
        }

        internal void MatchTarget()
        {
            var targetObject = target?.Get(this);
            if (targetObject == null) return;

            var targetTransform = targetObject.transform;
            if (matchPosition)
            {
                transform.position = targetTransform.position;
            }

            if (matchRotation)
            {
                transform.rotation = targetTransform.rotation;
            }

            if (matchScale)
            {
                var targetMatrix = targetTransform.localToWorldMatrix;
                var parentInverseMatrix = transform.parent?.worldToLocalMatrix ?? Matrix4x4.identity;
                var localTransformWithoutScale = Matrix4x4.TRS(
                    transform.localPosition,
                    transform.localRotation,
                    Vector3.one
                );
                transform.localScale = (localTransformWithoutScale * parentInverseMatrix * targetMatrix).lossyScale;
            }
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (target != null) yield return target;
        }
    }
}