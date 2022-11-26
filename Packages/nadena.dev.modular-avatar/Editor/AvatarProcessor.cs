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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [InitializeOnLoad]
    public class AvatarProcessor : IVRCSDKPreprocessAvatarCallback, IVRCSDKPostprocessAvatarCallback
    {
        public delegate void AvatarProcessorCallback(GameObject obj);

        public static event AvatarProcessorCallback AfterProcessing;

        static AvatarProcessor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Modular Avatar/Manual bake avatar", true)]
        private static bool ValidateApplyToCurrentAvatar()
        {
            var avatar = Selection.activeGameObject;
            return (avatar != null && avatar.GetComponent<VRCAvatarDescriptor>() != null);
        }

        [MenuItem("Tools/Modular Avatar/Manual bake avatar", false)]
        private static void ApplyToCurrentAvatar()
        {
            var avatar = Selection.activeGameObject;
            if (avatar == null || avatar.GetComponent<VRCAvatarDescriptor>() == null) return;
            var basePath = "Assets/ModularAvatarOutput/" + avatar.name;
            var savePath = basePath;

            int extension = 0;

            while (File.Exists(savePath) || Directory.Exists(savePath))
            {
                savePath = basePath + " " + (++extension);
            }

            try
            {
                Util.OverridePath = savePath;

                avatar = Object.Instantiate(avatar);
                avatar.transform.position += Vector3.forward * 2;
                ProcessAvatar(avatar);
            }
            finally
            {
                Util.OverridePath = null;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredEditMode)
            {
                Util.DeleteTemporaryAssets();
            }
        }

        public int callbackOrder => -9000;

        public void OnPostprocessAvatar()
        {
            Util.DeleteTemporaryAssets();
        }

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                ProcessAvatar(avatarGameObject);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        public static void ProcessAvatar(GameObject avatarGameObject)
        {
            BoneDatabase.ResetBones();
            PathMappings.Clear();

            try
            {
                new RenameParametersHook().OnPreprocessAvatar(avatarGameObject);
                new MenuInstallHook().OnPreprocessAvatar(avatarGameObject);
                new MergeArmatureHook().OnPreprocessAvatar(avatarGameObject);
                new RetargetMeshes().OnPreprocessAvatar(avatarGameObject);
                new BoneProxyProcessor().OnPreprocessAvatar(avatarGameObject);
                new VisibleHeadAccessoryProcessor(avatarGameObject.GetComponent<VRCAvatarDescriptor>()).Process();
                new MergeAnimatorProcessor().OnPreprocessAvatar(avatarGameObject);
                new BlendshapeSyncAnimationProcessor().OnPreprocessAvatar(avatarGameObject);

                AfterProcessing?.Invoke(avatarGameObject);
            }
            finally
            {
                // Ensure that we clean up AvatarTagComponents after failed processing. This ensures we don't re-enter
                // processing from the Awake method on the unprocessed AvatarTagComponents
                foreach (var component in avatarGameObject.GetComponentsInChildren<AvatarTagComponent>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            FixupAnimatorDebugData(avatarGameObject);
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private static void FixupAnimatorDebugData(GameObject avatarGameObject)
        {
            Object tempControlPanel = null;
            try
            {
                // The VRCSDK captures some debug information about animators as part of the build process, prior to invoking
                // hooks. For some reason this happens in the ValidateFeatures call on the SDK builder. Reinvoke it to
                // refresh this debug info.
                //
                // All of these methods are public, but for compatibility with unitypackage-based SDKs, we need to use
                // reflection to invoke everything here, as the asmdef structure is different between the two SDK variants.
                // Bleh.
                //
                // Canny filed requesting that this processing move after build hooks:
                // https://feedback.vrchat.com/sdk-bug-reports/p/animator-debug-information-needs-to-be-captured-after-invoking-preprocess-avatar
                var ty_VRCSdkControlPanelAvatarBuilder3A = Util.FindType(
                    "VRC.SDK3.Editor.VRCSdkControlPanelAvatarBuilder3A"
                );
                var ty_AvatarPerformanceStats = Util.FindType(
                    "VRC.SDKBase.Validation.Performance.Stats.AvatarPerformanceStats"
                );
                var ty_VRCSdkControlPanel = Util.FindType("VRCSdkControlPanel");
                tempControlPanel = ScriptableObject.CreateInstance(ty_VRCSdkControlPanel) as Object;

                var avatar = avatarGameObject.GetComponent<VRCAvatarDescriptor>();
                var animator = avatarGameObject.GetComponent<Animator>();
                var builder = ty_VRCSdkControlPanelAvatarBuilder3A.GetConstructor(Type.EmptyTypes)
                    .Invoke(Array.Empty<object>());
                var perfStats = ty_AvatarPerformanceStats.GetConstructor(new[] {typeof(bool)})
                    .Invoke(new object[] {false});
                ty_VRCSdkControlPanelAvatarBuilder3A
                    .GetMethod("RegisterBuilder", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(builder, new object[] {tempControlPanel});
                ty_VRCSdkControlPanelAvatarBuilder3A.GetMethod("ValidateFeatures").Invoke(
                    builder, new object[] {avatar, animator, perfStats}
                );
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[ModularAvatar] Incompatible VRCSDK version; failed to regenerate animator debug data");
                Debug.LogException(e);
            }
            finally
            {
                if (tempControlPanel != null) Object.DestroyImmediate(tempControlPanel);
            }
        }
    }
}