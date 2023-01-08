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
using JetBrains.Annotations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
#if UNITY_EDITOR
using System.Reflection;
#endif

namespace nadena.dev.modular_avatar.core
{
    internal static class RuntimeUtil
    {
        // Initialized in Util
        public static Action<Action> delayCall = (_) => { };
        public static event Action OnHierarchyChanged;

        public enum OnDemandSource
        {
            Awake,
            Start
        }

        public delegate void OnDemandProcessAvatarDelegate(OnDemandSource source, MonoBehaviour component);

        public static OnDemandProcessAvatarDelegate OnDemandProcessAvatar = (_m, _c) => { };

        [CanBeNull]
        public static string RelativePath(GameObject root, GameObject child)
        {
            if (root == child) return "";

            List<string> pathSegments = new List<string>();
            while (child != root && child != null)
            {
                pathSegments.Add(child.name);
                child = child.transform.parent?.gameObject;
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
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            }

            UnityEditor.EditorUtility.SetDirty(obj);
#endif
        }

#if UNITY_EDITOR
        private static UnityEngine.Object cachedAnimationWindowState;

        private static readonly Type animationWindowStateType
            = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.AnimationWindowState");

        private static readonly PropertyInfo recordingProp = animationWindowStateType.GetProperty(
            "recording",
            BindingFlags.Instance | BindingFlags.Public
        );

        private static readonly PropertyInfo previewingProp = animationWindowStateType.GetProperty(
            "previewing",
            BindingFlags.Instance | BindingFlags.Public
        );

        private static readonly PropertyInfo playingProp = animationWindowStateType.GetProperty(
            "playing",
            BindingFlags.Instance | BindingFlags.Public
        );
#endif

        public static bool IsAnimationEditMode()
        {
#if !UNITY_EDITOR
            return false;
#else
            if (cachedAnimationWindowState == null)
            {
                foreach (var obj in Resources.FindObjectsOfTypeAll(animationWindowStateType))
                {
                    cachedAnimationWindowState = obj;
                }
            }

            if (cachedAnimationWindowState == null) return false;

            return (bool) recordingProp.GetValue(cachedAnimationWindowState, null)
                   || (bool) previewingProp.GetValue(cachedAnimationWindowState, null)
                   || (bool) playingProp.GetValue(cachedAnimationWindowState, null);
#endif
        }

#if UNITY_EDITOR
        public static bool isPlaying => UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
#else
        public static bool isPlaying => true;
#endif
        public static void InvokeHierarchyChanged()
        {
            OnHierarchyChanged?.Invoke();
        }
    }
}
