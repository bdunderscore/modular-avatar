#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// See https://feedback.vrchat.com/sdk-bug-reports/p/ieditoronly-components-should-be-destroyed-late-in-the-build-process
    /// </summary>
    [InitializeOnLoad]
    internal static class PreventStripTagObjects
    {
        static PreventStripTagObjects()
        {
            EditorApplication.delayCall += () =>
            {
                var f_callbacks = typeof(VRCBuildPipelineCallbacks).GetField("_preprocessAvatarCallbacks",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var callbacks = (List<IVRCSDKPreprocessAvatarCallback>) f_callbacks.GetValue(null);

                var filteredCallbacks = callbacks.Where(c => !(c is RemoveAvatarEditorOnly)).ToList();

                f_callbacks.SetValue(null, filteredCallbacks);
            };
        }
    }

    internal class ReplacementRemoveAvatarEditorOnly : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -1024;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var xform in avatarGameObject.GetComponentsInChildren<Transform>(true))
            {
                if (xform != null && xform.CompareTag("EditorOnly"))
                {
                    Object.DestroyImmediate(xform.gameObject);
                }
            }

            return true;
        }
    }

    internal class ReplacementRemoveIEditorOnly : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => Int32.MaxValue;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var component in avatarGameObject.GetComponentsInChildren<IEditorOnly>(true))
            {
                Object.DestroyImmediate(component as Object);
            }

            return true;
        }
    }
}

#endif