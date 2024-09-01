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

#region

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.armature_lock;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

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
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/merge-armature?lang=auto")]
    public class ModularAvatarMergeArmature : AvatarTagComponent, IHaveObjReferences
    {
        public AvatarObjectReference mergeTarget = new AvatarObjectReference();
        public GameObject mergeTargetObject => mergeTarget.Get(this);

        public string prefix = "";
        public string suffix = "";

        [FormerlySerializedAs("locked")] public bool legacyLocked;

        public ArmatureLockMode LockMode = ArmatureLockMode.Legacy;

        public bool mangleNames = true;

        private ArmatureLockController _lockController;

        internal Transform MapBone(Transform bone)
        {
            var relPath = RuntimeUtil.RelativePath(gameObject, bone.gameObject);
            
            if (relPath == null) throw new ArgumentException("Bone is not a child of this component");
            if (relPath == "") return mergeTarget.Get(this).transform;
            
            var segments = relPath.Split('/');
            
            var pointer = mergeTarget.Get(this).transform;
            foreach (var segment in segments)
            {
                if (!segment.StartsWith(prefix) || !segment.EndsWith(suffix)
                                                || segment.Length == prefix.Length + suffix.Length) return null;
                var targetObjectName = segment.Substring(prefix.Length,
                    segment.Length - prefix.Length - suffix.Length);
                pointer = pointer.Find(targetObjectName);
            }

            return pointer;
        }
        
        internal Transform FindCorrespondingBone(Transform bone, Transform baseParent)
        {
            var childName = bone.gameObject.name;

            if (!childName.StartsWith(prefix) || !childName.EndsWith(suffix)
                                              || childName.Length == prefix.Length + suffix.Length) return null;
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

        internal void ResetArmatureLock()
        {
            if (_lockController != null)
            {
                _lockController.Dispose();
                _lockController = null;
            }

            SetLockMode();
        }

        internal void SetLockMode()
        {
            if (this == null) return;

            if (_lockController == null)
            {
                _lockController = ArmatureLockController.ForMerge(this, GetBonesForLock);
                _lockController.WhenUnstable += OnUnstableLock;
            }

            _lockController.Mode = LockMode;

            _lockController.Enabled = enabled;
        }

        private void OnUnstableLock()
        {
            _lockController.Mode = LockMode = ArmatureLockMode.NotLocked;
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
            // we use enabled instead of activeAndEnabled to ensure we track even when the GameObject is disabled
            _lockController.Enabled = enabled;
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
            if (this == null) return null;

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
                    var subMerge = t.GetComponent<ModularAvatarMergeArmature>();
                    if (subMerge != null && subMerge != this) continue;
                    
                    var baseChild = FindCorrespondingBone(t, baseBone);
                    if (baseChild != null)
                    {
                        mergeBones.Add((baseChild, t));
                        ScanHierarchy(t, baseChild);
                    }
                }
            }
        }

        public void InferPrefixSuffix()
        {
            // We only infer if targeting the armature (below the Hips bone)
            var rootAnimator = RuntimeUtil.FindAvatarTransformInParents(transform)?.GetComponent<Animator>();
            if (rootAnimator == null || !rootAnimator.isHuman) return;

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

            if (prefix == "J_Bip_C_")
            {
                // VRM workaround
                prefix = "J_Bip_";
            }

            if (!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix))
            {
                RuntimeUtil.MarkDirty(this);
            }
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            if (mergeTarget != null) yield return mergeTarget;
        }
    }
}