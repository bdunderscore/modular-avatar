using Codice.CM.WorkspaceServer.Tree;
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    [ExecuteInEditMode]
    public class MAInternalOffsetMarker : AvatarTagComponent
    {
        void OnValidate()
        {
#if MODULAR_AVATAR_DEBUG
            hideFlags = HideFlags.None;
#else
            hideFlags = HideFlags.HideInInspector;
#endif
        }

        private const float POS_EPSILON = 0.01f;
        private const float ROT_EPSILON = 0.01f;
        
        private Vector3 lastLocalPos;
        private Vector3 lastLocalScale;
        private Quaternion lastLocalRot;
        
        public Transform correspondingObject;

        public bool lockBasePosition;
        
        public void Update()
        {
            if (correspondingObject == null) return;

            // ReSharper disable once LocalVariableHidesMember
            var transform = this.transform;
            if ((transform.localPosition - lastLocalPos).sqrMagnitude > POS_EPSILON
                || (transform.localScale - lastLocalScale).sqrMagnitude > POS_EPSILON
                || Quaternion.Angle(lastLocalRot, transform.localRotation) > ROT_EPSILON)
            {
                if (lockBasePosition) transform.position = correspondingObject.position;
                else correspondingObject.localPosition = transform.localPosition;

                correspondingObject.localScale = transform.localScale;
                correspondingObject.localRotation = transform.localRotation;
            }
            else
            {
                if (lockBasePosition) transform.position = correspondingObject.position;
                else transform.localPosition = correspondingObject.localPosition;
                transform.localScale = correspondingObject.localScale;
                transform.localRotation = correspondingObject.localRotation;
            }

            lastLocalPos = transform.localPosition;
            lastLocalScale = transform.localScale;
            lastLocalRot = transform.localRotation;
        }
    }
}