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
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Probe Anchor")]
    public class ModularAvatarProbeAnchor : AvatarTagComponent
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
                HumanBodyBones origBoneReference = boneReference;
                string origSubpath = subPath;
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

        private void OnValidate()
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

            VRC.SDK3.Avatars.Components.VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(transform);
            if (avatar == null) return null;

            if (subPath == "$$AVATAR")
            {
                return avatar.transform;
            }

            if (boneReference == HumanBodyBones.LastBone)
            {
                return avatar.transform.Find(subPath);
            }

            Animator animator = avatar.GetComponent<Animator>();
            if (animator == null) return null;
            Transform bone = animator.GetBoneTransform(boneReference);
            if (bone == null) return null;
            if (string.IsNullOrWhiteSpace(subPath)) return bone;
            else return bone.Find(subPath);
        }

        private void UpdateStaticMapping(Transform newTarget)
        {
            VRC.SDK3.Avatars.Components.VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(transform);
            Dictionary<Transform, HumanBodyBones> humanBones = new Dictionary<Transform, HumanBodyBones>();
            Animator animator = avatar.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            foreach (object boneTypeObj in Enum.GetValues(typeof(HumanBodyBones)))
            {
                HumanBodyBones boneType = (HumanBodyBones)boneTypeObj;
                if (boneType == HumanBodyBones.LastBone) continue;
                Transform bone = animator.GetBoneTransform(boneType);
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