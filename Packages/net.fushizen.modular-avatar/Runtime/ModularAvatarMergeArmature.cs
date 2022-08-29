using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

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