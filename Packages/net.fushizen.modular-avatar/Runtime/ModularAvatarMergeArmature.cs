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
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    [ExecuteInEditMode]
    public class ModularAvatarMergeArmature : AvatarTagComponent
    {
        public GameObject mergeTarget;
        public string mergeTargetPath;
        public string prefix;
        public string suffix;
        public bool locked;

        private bool wasLocked;
#if UNITY_EDITOR
        void OnValidate()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (mergeTarget == null && !string.IsNullOrWhiteSpace(mergeTargetPath))
                {
                    var avatar = RuntimeUtil.FindAvatarInParents(transform);
                    if (avatar != null)
                    {
                        mergeTarget = avatar.transform.Find(mergeTargetPath)?.gameObject;
                    }
                    if (mergeTarget != null) {
                        RuntimeUtil.MarkDirty(this);
                    }
                }
                else if (mergeTarget != null && mergeTargetPath != RuntimeUtil.AvatarRootPath(mergeTarget))
                {
                    mergeTargetPath = RuntimeUtil.AvatarRootPath(mergeTarget);
                    int insetPos = gameObject.name.IndexOf(mergeTarget.name, StringComparison.Ordinal);
                    if (insetPos != -1)
                    {
                        prefix = gameObject.name.Substring(0, insetPos);
                        suffix = gameObject.name.Substring(insetPos + mergeTarget.name.Length);
                    }
                }

                CheckLock();
            };
        }
#endif

        void CheckLock()
        {
            if (RuntimeUtil.isPlaying) return;

            if (locked != wasLocked)
            {
                if (!locked)
                {
                    foreach (var comp in GetComponentsInChildren<MAInternalOffsetMarker>())
                    {
                        DestroyImmediate(comp);
                    }

                    wasLocked = false;
                }
                else
                {
                    if (mergeTarget == null) return;
                    foreach (var xform in GetComponentsInChildren<Transform>(true))
                    {
                        Transform baseObject = FindCorresponding(xform);
                        if (baseObject != null && xform.gameObject.GetComponent<MAInternalOffsetMarker>() == null)
                        {
                            var comp = xform.gameObject.AddComponent<MAInternalOffsetMarker>();
                            comp.correspondingObject = baseObject;
                            comp.lockBasePosition = baseObject.gameObject == mergeTarget;
                        }
                    }

                    wasLocked = true;
                }
            }
        }

        private Transform FindCorresponding(Transform xform)
        {
            if (xform == null) return null;
            if (xform == transform) return mergeTarget.transform;

            var correspondingParent = FindCorresponding(xform.parent);
            if (correspondingParent == null) return null;
            
            return correspondingParent.Find(prefix + xform.name + suffix);
        }
    }
}