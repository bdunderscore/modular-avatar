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
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace nadena.dev.modular_avatar.core
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Merge Armature")]
    public class ModularAvatarMergeArmature : AvatarTagComponent
    {
        private const float POS_EPSILON = 0.001f * 0.001f;
        private const float ROT_EPSILON = 0.001f * 0.001f;

        public AvatarObjectReference mergeTarget;
        public GameObject mergeTargetObject => mergeTarget.Get(this);

        public string prefix;
        public string suffix;
        public bool locked;

        private class BoneBinding
        {
            public Transform baseBone;
            public Transform mergeBone;

            public Vector3 lastLocalPos;
            public Vector3 lastLocalScale;
            public Quaternion lastLocalRot;
        }

        private List<BoneBinding> lockedBones;

        void OnValidate()
        {
            RuntimeUtil.delayCall(() =>
            {
                if (this == null) return;

                CheckLock();
            });
        }

        private void OnEnable()
        {
            RuntimeUtil.delayCall(CheckLock);
        }

        private void OnDisable()
        {
            RuntimeUtil.delayCall(CheckLock);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
        }

        void EditorUpdate()
        {
            if (this == null)
            {
#if UNITY_EDITOR
                EditorApplication.update -= EditorUpdate;
#endif
                return;
            }

            if (!locked || lockedBones == null)
            {
                CheckLock();
            }

            if (lockedBones != null)
            {
                foreach (var bone in lockedBones)
                {
                    if (bone.baseBone == null || bone.mergeBone == null)
                    {
                        lockedBones = null;
                        break;
                    }

                    var mergeBone = bone.mergeBone;
                    var correspondingObject = bone.baseBone;
                    bool lockBasePosition = bone.baseBone == mergeTargetObject.transform;

                    if ((mergeBone.localPosition - bone.lastLocalPos).sqrMagnitude > POS_EPSILON
                        || (mergeBone.localScale - bone.lastLocalScale).sqrMagnitude > POS_EPSILON
                        || Quaternion.Angle(bone.lastLocalRot, mergeBone.localRotation) > ROT_EPSILON)
                    {
                        if (lockBasePosition) mergeBone.position = correspondingObject.position;
                        else correspondingObject.localPosition = mergeBone.localPosition;

                        correspondingObject.localScale = mergeBone.localScale;
                        correspondingObject.localRotation = mergeBone.localRotation;
                    }
                    else
                    {
                        if (lockBasePosition) mergeBone.position = correspondingObject.position;
                        else mergeBone.localPosition = correspondingObject.localPosition;
                        mergeBone.localScale = correspondingObject.localScale;
                        mergeBone.localRotation = correspondingObject.localRotation;
                    }

                    bone.lastLocalPos = mergeBone.localPosition;
                    bone.lastLocalScale = mergeBone.localScale;
                    bone.lastLocalRot = mergeBone.localRotation;
                }
            }
        }

        void CheckLock()
        {
            if (RuntimeUtil.isPlaying) return;

#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            bool shouldLock = locked && isActiveAndEnabled;
            bool wasLocked = lockedBones != null;
            if (shouldLock != wasLocked)
            {
                if (!shouldLock)
                {
                    lockedBones = null;
                }
                else
                {
                    if (mergeTargetObject == null) return;
                    lockedBones = new List<BoneBinding>();

                    foreach (var xform in GetComponentsInChildren<Transform>(true))
                    {
                        Transform baseObject = FindCorresponding(xform);

                        if (baseObject == null) continue;

                        lockedBones.Add(new BoneBinding()
                        {
                            baseBone = baseObject,
                            mergeBone = xform,
                            lastLocalPos = xform.localPosition,
                            lastLocalScale = xform.localScale,
                            lastLocalRot = xform.localRotation
                        });
                    }
                }
            }

#if UNITY_EDITOR
            if (shouldLock)
            {
                EditorApplication.update += EditorUpdate;
            }
#endif
        }

        private Transform FindCorresponding(Transform xform)
        {
            if (xform == null) return null;
            if (xform == transform) return mergeTargetObject.transform;

            var correspondingParent = FindCorresponding(xform.parent);
            if (correspondingParent == null) return null;

            return correspondingParent.Find(prefix + xform.name + suffix);
        }

        public void InferPrefixSuffix()
        {
            // We only infer if targeting the armature (below the Hips bone)
            var rootAnimator = RuntimeUtil.FindAvatarInParents(transform)?.GetComponent<Animator>();
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