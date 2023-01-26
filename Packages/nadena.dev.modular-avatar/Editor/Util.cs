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
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
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
    internal static class Util
    {
        private const string generatedAssetsSubdirectory = "999_Modular_Avatar_Generated";
        private const string generatedAssetsPath = "Assets/" + generatedAssetsSubdirectory;

        [CanBeNull] public static string OverridePath;

        static Util()
        {
            RuntimeUtil.delayCall = (cb) => EditorApplication.delayCall += cb.Invoke;

            EditorApplication.hierarchyChanged += () => { RuntimeUtil.InvokeHierarchyChanged(); };
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

        public static bool IsTemporaryAsset(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);

            return path != null && path.StartsWith(GetGeneratedAssetsFolder() + "/");
        }

        public static Type FindType(string typeName)
        {
            Type avatarValidation = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                avatarValidation = assembly.GetType(typeName);
                if (avatarValidation != null)
                {
                    break;
                }
            }

            return avatarValidation;
        }

        private const int MAX_EXPRESSION_TEXTURE_SIZE = 256;

        public enum ValidateExpressionMenuIconResult
        {
            Success,
            TooLarge,
            Uncompressed
        }

        public static ValidateExpressionMenuIconResult ValidateExpressionMenuIcon(Texture2D icon)
        {
            string path = AssetDatabase.GetAssetPath(icon);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return ValidateExpressionMenuIconResult.Success;
            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();

            // Max texture size;
            if ((icon.width > MAX_EXPRESSION_TEXTURE_SIZE || icon.height > MAX_EXPRESSION_TEXTURE_SIZE) &&
                settings.maxTextureSize > MAX_EXPRESSION_TEXTURE_SIZE) return ValidateExpressionMenuIconResult.TooLarge;

            // Compression
            if (settings.textureCompression == TextureImporterCompression.Uncompressed)
                return ValidateExpressionMenuIconResult.Uncompressed;
            return ValidateExpressionMenuIconResult.Success;
        }

        internal static IEnumerable<AnimatorState> States(AnimatorController ac)
        {
            HashSet<AnimatorStateMachine> visitedStateMachines = new HashSet<AnimatorStateMachine>();
            Queue<AnimatorStateMachine> pending = new Queue<AnimatorStateMachine>();

            foreach (var layer in ac.layers)
            {
                if (layer.stateMachine != null) pending.Enqueue(layer.stateMachine);
            }

            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (visitedStateMachines.Contains(next)) continue;
                visitedStateMachines.Add(next);

                foreach (var child in next.stateMachines)
                {
                    if (child.stateMachine != null) pending.Enqueue(child.stateMachine);
                }

                foreach (var state in next.states)
                {
                    yield return state.state;
                }
            }
        }

        internal static bool IsProxyAnimation(Motion m)
        {
            var path = AssetDatabase.GetAssetPath(m);

            // This is a fairly wide condition in order to deal with:
            // 1. Future additions of proxy animations (so GUIDs are out)
            // 2. Unitypackage based installations of the VRCSDK
            // 3. VCC based installations of the VRCSDK
            // 4. Very old VCC based installations of the VRCSDK where proxy animations were copied into Assets
            return path.Contains("/AV3 Demo Assets/Animation/ProxyAnim/proxy")
                   || path.Contains("/VRCSDK/Examples3/Animation/ProxyAnim/proxy");
        }
    }
}