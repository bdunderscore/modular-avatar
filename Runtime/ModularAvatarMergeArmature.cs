﻿/*
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
using nadena.dev.modular_avatar.core.armature_lock;
using UnityEngine;
using UnityEngine.Serialization;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public enum ArmatureLockMode
    {
        Legacy,
        NotLocked,
        BaseToMerge,
        BidirectionalExact
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Merge Armature")]
    public class ModularAvatarMergeArmature : AvatarTagComponent
    {
        public AvatarObjectReference mergeTarget = new AvatarObjectReference();
        public GameObject mergeTargetObject => mergeTarget.Get(this);

        public string prefix = "";
        public string suffix = "";

        [FormerlySerializedAs("locked")] public bool legacyLocked;

        public ArmatureLockMode LockMode = ArmatureLockMode.Legacy;

        public bool mangleNames = true;

        private ArmatureLockController _lockController;

        internal Transform FindCorrespondingBone(Transform bone, Transform baseParent)
        {
            var childName = bone.gameObject.name;

            if (!childName.StartsWith(prefix) || !childName.EndsWith(suffix)) return null;
            var targetObjectName = childName.Substring(prefix.Length,
                childName.Length - prefix.Length - suffix.Length);
            return baseParent.Find(targetObjectName);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            MigrateLockConfig();
            RuntimeUtil.delayCall(SetLockMode);
        }

        private void SetLockMode()
        {
            if (this == null) return;

            if (_lockController == null)
            {
                _lockController = ArmatureLockController.ForMerge(this, GetBonesForLock);
            }

            if (_lockController.Mode != LockMode)
            {
                _lockController.Mode = LockMode;

                if (!_lockController.IsStable())
                {
                    _lockController.Mode = LockMode = ArmatureLockMode.NotLocked;
                }
            }

            _lockController.Enabled = isActiveAndEnabled;
        }

        private void MigrateLockConfig()
        {
            if (LockMode == ArmatureLockMode.Legacy)
            {
                LockMode = legacyLocked ? ArmatureLockMode.BidirectionalExact : ArmatureLockMode.BaseToMerge;
            }
        }

        private void OnEnable()
        {
            MigrateLockConfig();

            SetLockMode();
        }

        private void OnDisable()
        {
            _lockController.Enabled = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _lockController?.Dispose();
            _lockController = null;
        }

        public override void ResolveReferences()
        {
            mergeTarget?.Get(this);
        }

        private List<(Transform, Transform)> GetBonesForLock()
        {
            var mergeRoot = this.transform;
            var baseRoot = mergeTarget.Get(this);

            if (baseRoot == null) return null;

            List<(Transform, Transform)> mergeBones = new List<(Transform, Transform)>();

            ScanHierarchy(mergeRoot, baseRoot.transform);

            return mergeBones;


            void ScanHierarchy(Transform merge, Transform baseBone)
            {
                foreach (Transform t in merge)
                {
                    var baseChild = FindCorrespondingBone(t, baseBone);
                    if (baseChild != null)
                    {
                        mergeBones.Add((t, baseChild));
                        ScanHierarchy(t, baseChild);
                    }
                }
            }
        }

        public void InferPrefixSuffix()
        {
            // We only infer if targeting the armature (below the Hips bone)
            var rootAnimator = RuntimeUtil.FindAvatarTransformInParents(transform)?.GetComponent<Animator>();
            if (rootAnimator == null) return;

            var hips = rootAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || hips.transform.parent != mergeTargetObject.transform) return;

            // We also require that the attached object has exactly one child (presumably the hips)
            if (transform.childCount != 1) return;

            // Infer the prefix and suffix by comparing the names of the mergeTargetObject's hips with the child of the
            // GameObject we're attached to.
            var baseName = hips.name;
            var mergeName = transform.GetChild(0).name;

            var prefixLength = mergeName.IndexOf(baseName, StringComparison.InvariantCulture);
            if (prefixLength < 0) return;

            var suffixLength = mergeName.Length - prefixLength - baseName.Length;

            prefix = mergeName.Substring(0, prefixLength);
            suffix = mergeName.Substring(mergeName.Length - suffixLength);

            if (!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix))
            {
                RuntimeUtil.MarkDirty(this);
            }
        }
    }
}