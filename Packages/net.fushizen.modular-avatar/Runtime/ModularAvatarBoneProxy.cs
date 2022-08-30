using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using Object = System.Object;

namespace net.fushizen.modular_avatar.core
{
    public class ModularAvatarBoneProxy : AvatarTagComponent
    {
        public Transform target;
        
        public HumanBodyBones boneReference = HumanBodyBones.LastBone;
        public string subPath;

        [SerializeField] [HideInInspector] public ParentConstraint constraint;

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += CheckReferences;
        }

        void CheckReferences() {
            if (this == null) return; // post-destroy
            
            if (target == null && (boneReference != HumanBodyBones.LastBone || !string.IsNullOrWhiteSpace(subPath)))
            {
                UpdateDynamicMapping();
                if (target != null)
                {
                    RuntimeUtil.MarkDirty(this);
                }
            } else if (target != null)
            {
                var origBoneReference = boneReference;
                var origSubpath = subPath;
                UpdateStaticMapping();
                if (origSubpath != subPath || origBoneReference != boneReference)
                {
                    RuntimeUtil.MarkDirty(this);
                }
            }
            
            CheckConstraint();
        }

        private void CheckConstraint()
        {
            if (target != null)
            {
                if (constraint == null)
                {
                    constraint = gameObject.AddComponent<ParentConstraint>();
                    constraint.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                    constraint.AddSource(new ConstraintSource()
                    {
                        weight = 1,
                        sourceTransform = target
                    });
                    constraint.translationOffsets = new Vector3[] {Vector3.zero};
                    constraint.rotationOffsets = new Vector3[] {Vector3.zero};
                    constraint.locked = true;
                    constraint.constraintActive = true;
                }
                else
                {
                    constraint.SetSource(0, new ConstraintSource()
                    {
                        weight = 1,
                        sourceTransform = target
                    });
                }
            }
        }

        private void OnDestroy()
        {
            if (constraint != null) DestroyImmediate(constraint);
        }

        private void UpdateDynamicMapping()
        {
            var avatar = RuntimeUtil.FindAvatarInParents(transform);
            if (avatar == null) return;

            if (subPath == "$$AVATAR")
            {
                target = avatar.transform;
                return;
            }

            if (boneReference == HumanBodyBones.LastBone)
            {
                target = avatar.transform.Find(subPath);
                return;
            }

            var animator = avatar.GetComponent<Animator>();
            if (animator == null) return;
            var bone = animator.GetBoneTransform(boneReference);
            if (bone == null) return;
            if (string.IsNullOrWhiteSpace(subPath)) target = bone;
            else target = bone.Find(subPath);
        }

        private void UpdateStaticMapping()
        {
            var avatar = RuntimeUtil.FindAvatarInParents(transform);
            var humanBones = new Dictionary<Transform, HumanBodyBones>();
            var animator = avatar.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            foreach (var boneTypeObj in Enum.GetValues(typeof(HumanBodyBones)))
            {
                var boneType = (HumanBodyBones) boneTypeObj;
                if (boneType == HumanBodyBones.LastBone) continue;
                var bone = animator.GetBoneTransform(boneType);
                if (bone != null) humanBones[bone] = boneType;
            }

            Transform iter = target;
            Transform avatarTransform = avatar.transform;

            if (target == avatarTransform)
            {
                boneReference = HumanBodyBones.LastBone;
                subPath = "$$AVATAR";
                return;
            }

            while (iter != avatarTransform && !humanBones.ContainsKey(iter))
            {
                iter = iter.parent;
            }

            if (iter == avatarTransform)
            {
                boneReference = HumanBodyBones.LastBone;
            }
            else
            {
                boneReference = humanBones[iter];
            }

            subPath = RuntimeUtil.RelativePath(iter.gameObject, target.gameObject);
        }
#endif
    }
}