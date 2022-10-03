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

using System.Collections.Generic;
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    [ExecuteInEditMode]
    public class ModularAvatarMergeArmature : AvatarTagComponent
    {
        private const float POS_EPSILON = 0.01f;
        private const float ROT_EPSILON = 0.01f;

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
            if (this == null || lockedBones == null)
            {
#if UNITY_EDITOR
                EditorApplication.update -= EditorUpdate;
#endif
                return;
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
            if (locked)
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
    }
}