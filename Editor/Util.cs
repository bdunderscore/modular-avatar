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
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [InitializeOnLoad]
    internal static class Util
    {
        static Util()
        {
            RuntimeUtil.delayCall = (cb) => EditorApplication.delayCall += cb.Invoke;

            EditorApplication.hierarchyChanged += () => { RuntimeUtil.InvokeHierarchyChanged(); };

            EditorApplication.update += DisableMAGizmoIcons;
        }

        // From Acegikmo http://answers.unity.com/answers/1722605/view.html
        // In Unity 2022.1+, this can be replaced with GizmoUtility.SetIconEnabled(type, enabled);
        static MethodInfo setIconEnabled;

        static MethodInfo SetIconEnabled => setIconEnabled = setIconEnabled ?? Assembly.GetAssembly(typeof(Editor))
            ?.GetType("UnityEditor.AnnotationUtility")
            ?.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

        private static MethodInfo getAnnotations;

        private static MethodInfo GetAnnotations =>
            getAnnotations = getAnnotations ??
                             Assembly.GetAssembly(typeof(Editor))
                                 ?.GetType("UnityEditor.AnnotationUtility")
                                 ?.GetMethod("GetAnnotations", BindingFlags.Static | BindingFlags.NonPublic);

        private static Type t_Annotation = Assembly.GetAssembly(typeof(Editor))?.GetType("UnityEditor.Annotation");

        private static FieldInfo f_classID =
            t_Annotation?.GetField("classID", BindingFlags.Instance | BindingFlags.Public);

        private static FieldInfo f_scriptClass =
            t_Annotation?.GetField("scriptClass", BindingFlags.Instance | BindingFlags.Public);

        static void SetGizmoIconEnabled(Type type, bool enabled)
        {
            if (SetIconEnabled == null) return;
            const int MONO_BEHAVIOR_CLASS_ID = 114; // https://docs.unity3d.com/Manual/ClassIDReference.html
            SetIconEnabled.Invoke(null, new object[] {MONO_BEHAVIOR_CLASS_ID, type.Name, enabled ? 1 : 0});
        }

        static void DisableMAGizmoIcons()
        {
            if (SessionState.GetBool("MAIconsDisabled", false) ||
                f_classID == null || f_scriptClass == null || GetAnnotations == null || SetIconEnabled == null)
            {
                EditorApplication.update -= DisableMAGizmoIcons;
                SessionState.GetBool("MAIconsDisabled", true);
                return;
            }


            var annotations = (Array) GetAnnotations.Invoke(null, new object[] { });
            bool hasBoneProxy = false;
            for (int i = 0; i < annotations.Length; i++)
            {
                var annotation = annotations.GetValue(i);
                var classID = (int) f_classID.GetValue(annotation);
                var scriptClass = (string) f_scriptClass.GetValue(annotation);

                if (classID == 114 && scriptClass == "ModularAvatarBoneProxy")
                {
                    hasBoneProxy = true;
                    break;
                }
            }

            if (!hasBoneProxy)
            {
                // Annotations aren't created yet for MA types, check back later.
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var ty in assembly.GetTypes())
                {
                    if (typeof(AvatarTagComponent).IsAssignableFrom(ty) && !ty.IsAbstract)
                    {
                        SetGizmoIconEnabled(ty, false);
                    }
                }
            }

            EditorApplication.update -= DisableMAGizmoIcons;
            SessionState.GetBool("MAIconsDisabled", true);
        }

        internal static T GetOrAddComponent<T>(this Component self) where T : Component
        {
            var component = self.GetComponent<T>();
            if (component == null) component = self.gameObject.AddComponent<T>();
            return component;
        }

        internal static T GetOrAddComponent<T>(this GameObject self) where T : Component
        {
            var component = self.GetComponent<T>();
            if (component == null) component = self.AddComponent<T>();
            return component;
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

        internal static IEnumerable<T> FindComponentInParents<T>(this Component t) where T : Component
        {
            Transform ptr = t.transform.parent;
            while (ptr != null)
            {
                var component = ptr.GetComponent<T>();
                if (component != null) yield return component;
                if (RuntimeUtil.IsAvatarRoot(ptr)) break;
                ptr = ptr.parent;
            }
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

        public static T LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path)) return null;

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}