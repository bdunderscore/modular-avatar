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

using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    internal class CleanupTempAssets : IVRCSDKPostprocessAvatarCallback
    {
        public int callbackOrder => 99999;

        public void OnPostprocessAvatar()
        {
            Util.DeleteTemporaryAssets();
        }
    }

    [InitializeOnLoad]
    public static class Util
    {
        private const string generatedAssetsSubdirectory = "999_Modular_Avatar_Generated";
        private const string generatedAssetsPath = "Assets/" + generatedAssetsSubdirectory;

        [CanBeNull] public static string OverridePath;

        static Util()
        {
            RuntimeUtil.delayCall = (cb) => EditorApplication.delayCall += cb.Invoke;

            EditorApplication.hierarchyChanged += () => { RuntimeUtil.InvokeHierarchyChanged(); };
        }

        public static AnimatorController CreateAnimator(AnimatorController toClone = null)
        {
            AnimatorController controller;
            if (toClone != null)
            {
                controller = Object.Instantiate(toClone);
            }
            else
            {
                controller = new AnimatorController();
            }

            AssetDatabase.CreateAsset(controller, GenerateAssetPath());

            return controller;
        }

        public static string GenerateAssetPath()
        {
            return GetGeneratedAssetsFolder() + "/" + GUID.Generate() + ".asset";
        }

        private static string GetGeneratedAssetsFolder()
        {
            var path = OverridePath ?? generatedAssetsPath;

            var pathParts = path.Split('/');

            for (int i = 1; i < pathParts.Length; i++)
            {
                var subPath = string.Join("/", pathParts, 0, i + 1);
                if (!AssetDatabase.IsValidFolder(subPath))
                {
                    AssetDatabase.CreateFolder(string.Join("/", pathParts, 0, i), pathParts[i]);
                }
            }

            return path;
        }

        internal static void DeleteTemporaryAssets()
        {
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.SaveAssets();

                var subdir = generatedAssetsPath;

                AssetDatabase.DeleteAsset(subdir);
                FileUtil.DeleteFileOrDirectory(subdir);
            };
        }

        public static AnimatorController DeepCloneAnimator(AnimatorController controller)
        {
            var merger = new AnimatorCombiner();
            merger.AddController("", controller, null);
            return merger.Finish();
        }

        public static bool IsTemporaryAsset(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);

            return path != null && path.StartsWith(GetGeneratedAssetsFolder() + "/");
        }
    }
}