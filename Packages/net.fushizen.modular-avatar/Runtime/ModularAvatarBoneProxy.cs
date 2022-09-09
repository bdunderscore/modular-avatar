/*
 * MIT License
 * 
 * Copyright (c) 2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

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
            UnityEditor.EditorApplication.delayCall += () => { if (constraint != null) DestroyImmediate(constraint); };
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