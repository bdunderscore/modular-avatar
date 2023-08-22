﻿/*
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using BuildReport = nadena.dev.modular_avatar.editor.ErrorReporting.BuildReport;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("Tests")]

namespace nadena.dev.modular_avatar.core.editor
{
    [InitializeOnLoad]
    public class AvatarProcessor : IVRCSDKPreprocessAvatarCallback, IVRCSDKPostprocessAvatarCallback
    {
        // Place after EditorOnly processing (which runs at -1024) but hopefully before most other user callbacks
        public int callbackOrder => -25;

        /// <summary>
        /// Avoid recursive activation of avatar processing by suppressing starting processing while processing is
        /// already in progress.
        /// </summary>
        private static bool nowProcessing = false;

        internal delegate void AvatarProcessorCallback(GameObject obj, BuildContext context);

        /// <summary>
        /// This API is NOT stable. Do not use it yet.
        /// </summary>
        internal static event AvatarProcessorCallback AfterProcessing;

        static AvatarProcessor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("GameObject/ModularAvatar/Manual bake avatar", true, 100)]
        static bool ValidateApplyToCurrentAvatarGameobject()
        {
            return ValidateApplyToCurrentAvatar();
        }

        [MenuItem("GameObject/ModularAvatar/Manual bake avatar", false, 100)]
        static void ApplyToCurrentAvatarGameobject()
        {
            ApplyToCurrentAvatar();
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

            string originalBasePath = RuntimeUtil.RelativePath(null, avatar);
            avatar = Object.Instantiate(avatar);

            string clonedBasePath = RuntimeUtil.RelativePath(null, avatar);
            try
            {
                Util.OverridePath = savePath;

                var original = avatar;
                avatar.transform.position += Vector3.forward * 2;

                BuildReport.Clear();

                ProcessAvatar(avatar);
                Selection.objects = new Object[] {avatar};
            }
            finally
            {
                Util.OverridePath = null;
                BuildReport.RemapPaths(originalBasePath, clonedBasePath);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredEditMode)
            {
                Util.DeleteTemporaryAssets();
            }
        }

        public void OnPostprocessAvatar()
        {
            Util.DeleteTemporaryAssets();
        }

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                BuildReport.Clear();
                ProcessAvatar(avatarGameObject);
                FixupAnimatorDebugData(avatarGameObject);
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
            if (nowProcessing) return;

            var vrcAvatarDescriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            using (BuildReport.CurrentReport.ReportingOnAvatar(vrcAvatarDescriptor))
            {
                try
                {
                    try
                    {
                        AssetDatabase.StartAssetEditing();
                        nowProcessing = true;

                        RemoveMissingScriptComponents(avatarGameObject);

                        ClearEditorOnlyTagComponents(avatarGameObject.transform);

                        BoneDatabase.ResetBones();
                        PathMappings.Init(vrcAvatarDescriptor.gameObject);
                        ClonedMenuMappings.Clear();

                        // Sometimes people like to nest one avatar in another, when transplanting clothing. To avoid issues
                        // with inconsistently determining the avatar root, we'll go ahead and remove the extra sub-avatars
                        // here.
                        foreach (Transform directChild in avatarGameObject.transform)
                        {
                            foreach (var component in directChild.GetComponentsInChildren<VRCAvatarDescriptor>(true))
                            {
                                Object.DestroyImmediate(component);
                            }

                            // Disable deprecation warning for reference to PipelineSaver
#pragma warning disable CS0618
                            foreach (var component in directChild.GetComponentsInChildren<PipelineSaver>(true))
#pragma warning restore CS0618
                            {
                                Object.DestroyImmediate(component);
                            }
                        }

                        var context = new BuildContext(vrcAvatarDescriptor);

                        new MeshSettingsPass(context).OnPreprocessAvatar();
                        new RenameParametersHook().OnPreprocessAvatar(avatarGameObject, context);
                        new MergeAnimatorProcessor().OnPreprocessAvatar(avatarGameObject, context);
                        context.AnimationDatabase.Bootstrap(vrcAvatarDescriptor);

                        new MenuInstallHook().OnPreprocessAvatar(avatarGameObject, context);
                        new MergeArmatureHook().OnPreprocessAvatar(context, avatarGameObject);
                        new BoneProxyProcessor().OnPreprocessAvatar(avatarGameObject);
                        new VisibleHeadAccessoryProcessor(vrcAvatarDescriptor).Process(context);
                        new ReplaceObjectPass(context).Process();
                        new RemapAnimationPass(vrcAvatarDescriptor).Process(context.AnimationDatabase);
                        new BlendshapeSyncAnimationProcessor().OnPreprocessAvatar(avatarGameObject, context);
                        PhysboneBlockerPass.Process(avatarGameObject);

                        context.CommitReferencedAssets();

                        AfterProcessing?.Invoke(avatarGameObject, context);

                        context.AnimationDatabase.Commit();

                        new GCGameObjectsPass(context, avatarGameObject).OnPreprocessAvatar();

                        context.CommitReferencedAssets();
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();

                        nowProcessing = false;

                        // Ensure that we clean up AvatarTagComponents after failed processing. This ensures we don't re-enter
                        // processing from the Awake method on the unprocessed AvatarTagComponents
                        var toDestroy = avatarGameObject.GetComponentsInChildren<AvatarTagComponent>(true).ToList();
                        var retryDestroy = new List<AvatarTagComponent>();

                        // Sometimes AvatarTagComponents have interdependencies and need to be deleted in the right order;
                        // retry until we purge them all.
                        bool madeProgress = true;
                        while (toDestroy.Count > 0)
                        {
                            if (!madeProgress)
                            {
                                throw new Exception("One or more components failed to destroy." +
                                                    RuntimeUtil.AvatarRootPath(toDestroy[0].gameObject));
                            }

                            foreach (var component in toDestroy)
                            {
                                try
                                {
                                    if (component != null)
                                    {
                                        UnityEngine.Object.DestroyImmediate(component);
                                        madeProgress = true;
                                    }
                                }
                                catch (Exception)
                                {
                                    retryDestroy.Add(component);
                                }
                            }

                            toDestroy = retryDestroy;
                            retryDestroy = new List<AvatarTagComponent>();
                        }

                        var activator = avatarGameObject.GetComponent<AvatarActivator>();
                        if (activator != null)
                        {
                            UnityEngine.Object.DestroyImmediate(activator);
                        }

                        ClonedMenuMappings.Clear();

                        AssetDatabase.SaveAssets();

                        Resources.UnloadUnusedAssets();
                    }
                }
                catch (Exception e)
                {
                    BuildReport.LogException(e);
                    throw;
                }
                finally
                {
                    ErrorReportUI.MaybeOpenErrorReportUI();
                }

                if (!BuildReport.CurrentReport.CurrentAvatar.successful)
                {
                    throw new Exception("Fatal error reported during avatar processing.");
                }
            }

            Debug.Log($"Processed avatar " + avatarGameObject.name + " in " + sw.ElapsedMilliseconds + "ms");
        }

        private static void RemoveMissingScriptComponents(GameObject avatarGameObject)
        {
            foreach (var child in avatarGameObject.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }

        private static void ClearEditorOnlyTagComponents(Transform obj)
        {
            // EditorOnly objects can be used for multiple purposes - users might want a camera rig to be available in
            // play mode, for example. For now, we'll prune MA components from EditorOnly objects, but otherwise leave
            // them in place when in play mode.

            if (obj.CompareTag("EditorOnly"))
            {
                foreach (var component in obj.GetComponentsInChildren<AvatarTagComponent>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
            else
            {
                foreach (Transform transform in obj)
                {
                    ClearEditorOnlyTagComponents(transform);
                }
            }
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

                if (ty_VRCSdkControlPanelAvatarBuilder3A == null || ty_AvatarPerformanceStats == null ||
                    ty_VRCSdkControlPanel == null)
                {
                    return;
                }

                var avatar = avatarGameObject.GetComponent<VRCAvatarDescriptor>();
                var animator = avatarGameObject.GetComponent<Animator>();
                var builder = ty_VRCSdkControlPanelAvatarBuilder3A.GetConstructor(Type.EmptyTypes)
                    ?.Invoke(Array.Empty<object>());
                var perfStats = ty_AvatarPerformanceStats.GetConstructor(new[] {typeof(bool)})
                    ?.Invoke(new object[] {false});

                if (builder == null || perfStats == null)
                {
                    return;
                }

                tempControlPanel = ScriptableObject.CreateInstance(ty_VRCSdkControlPanel) as Object;

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
