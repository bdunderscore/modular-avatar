using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.modular_avatar.core
{
    public static class RuntimeUtil
    {
        [CanBeNull]
        public static string RelativePath(GameObject root, GameObject child)
        {
            if (root == child) return "";
            
            List<string> pathSegments = new List<string>();
            while (child != root && child != null)
            {
                pathSegments.Add(child.name);
                child = child.transform.parent.gameObject;
            }

            if (child == null) return null;
            
            pathSegments.Reverse();
            return String.Join("/", pathSegments);
        }

        [CanBeNull]
        public static string AvatarRootPath(GameObject child)
        {
            var avatar = FindAvatarInParents(child.transform);
            if (avatar == null) return null;
            return RelativePath(avatar.gameObject, child);
        }

        public static VRCAvatarDescriptor FindAvatarInParents(Transform target)
        {
            while (target != null)
            {
                var av = target.GetComponent<VRCAvatarDescriptor>();
                if (av != null) return av;
                target = target.parent;
            }

            return null;
        }

        public static void MarkDirty(UnityEngine.Object obj)
        {
            #if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            }
            EditorUtility.SetDirty(obj);
            #endif
        }
    }
}