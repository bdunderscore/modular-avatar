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

namespace net.fushizen.modular_avatar.core
{
    [ExecuteInEditMode]
    public class ModularAvatarBoneProxy : AvatarTagComponent
    {
        private Transform _targetCache;

        public Transform target
        {
            get
            {
                if (_targetCache != null) return _targetCache;
                UpdateDynamicMapping();
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

        void OnValidate()
        {
            ClearCache();
        }

        void ClearCache()
        {
            _targetCache = null;
            RuntimeUtil.OnHierarchyChanged -= ClearCache;
        }

        private void Update()
        {
            if (!RuntimeUtil.isPlaying && target != null)
            {
                var targetTransform = target.transform;
                var myTransform = transform;
                myTransform.position = targetTransform.position;
                myTransform.rotation = targetTransform.rotation;
            }
        }

        private void OnDestroy()
        {
            RuntimeUtil.OnHierarchyChanged -= ClearCache;
        }

        private void UpdateDynamicMapping()
        {
            if (boneReference == HumanBodyBones.LastBone)
            {
                return;
            }

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
            if (string.IsNullOrWhiteSpace(subPath)) _targetCache = bone;
            else _targetCache = bone.Find(subPath);
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