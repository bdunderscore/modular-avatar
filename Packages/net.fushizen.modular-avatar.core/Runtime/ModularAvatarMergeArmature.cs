using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    public class ModularAvatarMergeArmature : AvatarTagComponent
    {
        public GameObject mergeTarget;
        public string mergeTargetPath;
        public string prefix;
        public string suffix;
        
        void OnValidate()
        {
            EditorApplication.delayCall += () =>
            {
                if (mergeTarget == null && !string.IsNullOrWhiteSpace(mergeTargetPath))
                {
                    var avatar = RuntimeUtil.FindAvatarInParents(transform);
                    if (avatar != null)
                    {
                        mergeTarget = avatar.transform.Find(mergeTargetPath)?.gameObject;
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
            };
        }
    }
}