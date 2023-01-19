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

namespace nadena.dev.modular_avatar.core
{
    public enum BoneProxyAttachmentMode
    {
        /// <summary>
        /// Initial state - this will be updated automatically by the bone proxy inspector, based on checking whether
        /// the proxy is located near the base bone.
        ///
        /// If somehow we run a build with this still on default, we'll use AsChildAtRoot.
        /// </summary>
        Unset,

        /// <summary>
        /// Places the bone proxy object at the target, with localPosition and localRotation zeroed.
        /// </summary>
        AsChildAtRoot,

        /// <summary>
        /// Places the bone proxy object at the target, preserving world position and orientation.
        /// </summary>
        AsChildKeepWorldPose,

        /// <summary>
        /// Places the bone proxy object at the target, preserving local rotation only.
        /// </summary>
        AsChildKeepRotation,

        /// <summary>
        /// Places the bone proxy object at the target, preserving local position only.
        /// </summary>
        AsChildKeepPosition,
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Bone Proxy")]
    public class ModularAvatarBoneProxy : AvatarTagComponent
    {
        private Transform _targetCache;

        public Transform target
        {
            get
            {
                if (_targetCache != null) return _targetCache;
                _targetCache = UpdateDynamicMapping();
                RuntimeUtil.OnHierarchyChanged -= ClearCache;
                RuntimeUtil.OnHierarchyChanged += ClearCache;
                return _targetCache;
            }
            set
            {
                var origBoneReference = boneReference;
                var origSubpath = subPath;
                UpdateStaticMapping(value);
                if (origSubpath != subPath || origBoneReference != boneReference)
                {
                    RuntimeUtil.MarkDirty(this);
                }

                RuntimeUtil.OnHierarchyChanged -= ClearCache;
                RuntimeUtil.OnHierarchyChanged += ClearCache;
            }
        }

        public HumanBodyBones boneReference = HumanBodyBones.LastBone;
        public string subPath;
        public BoneProxyAttachmentMode attachmentMode = BoneProxyAttachmentMode.Unset;

        void OnValidate()
        {
            ClearCache();
        }

        internal void ClearCache()
        {
            _targetCache = null;
            RuntimeUtil.OnHierarchyChanged -= ClearCache;
        }

        internal void Update()
        {
            if (!RuntimeUtil.isPlaying && target != null)
            {
                var targetTransform = target.transform;
                var myTransform = transform;
                switch (attachmentMode)
                {
                    case BoneProxyAttachmentMode.AsChildAtRoot:
                        myTransform.position = targetTransform.position;
                        myTransform.rotation = targetTransform.rotation;
                        break;
                    case BoneProxyAttachmentMode.AsChildKeepPosition:
                        myTransform.rotation = targetTransform.rotation;
                        break;
                    case BoneProxyAttachmentMode.AsChildKeepRotation:
                        myTransform.position = targetTransform.position;
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            RuntimeUtil.OnHierarchyChanged -= ClearCache;
        }

        private Transform UpdateDynamicMapping()
        {
            if (boneReference == HumanBodyBones.LastBone && string.IsNullOrWhiteSpace(subPath))
            {
                return null;
            }

            var avatar = RuntimeUtil.FindAvatarInParents(transform);
            if (avatar == null) return null;

            if (subPath == "$$AVATAR")
            {
                return avatar.transform;
            }

            if (boneReference == HumanBodyBones.LastBone)
            {
                return avatar.transform.Find(subPath);
            }

            var animator = avatar.GetComponent<Animator>();
            if (animator == null) return null;
            var bone = animator.GetBoneTransform(boneReference);
            if (bone == null) return null;
            if (string.IsNullOrWhiteSpace(subPath)) return bone;
            else return bone.Find(subPath);
        }

        private void UpdateStaticMapping(Transform newTarget)
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

            Transform iter = newTarget;
            Transform avatarTransform = avatar.transform;

            if (newTarget == avatarTransform)
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

            subPath = RuntimeUtil.RelativePath(iter.gameObject, newTarget.gameObject);
            _targetCache = newTarget;
        }
    }
}