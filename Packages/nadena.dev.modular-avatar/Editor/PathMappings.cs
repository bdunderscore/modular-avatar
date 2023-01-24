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

using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class PathMappings
    {
        private static Dictionary<GameObject, List<string>> _objectToOriginalPaths =
            new Dictionary<GameObject, List<string>>();

        private static ImmutableDictionary<string, string> _originalPathToMappedPath = null;
        private static ImmutableDictionary<string, string> _transformOriginalPathToMappedPath = null;

        private static HashSet<GameObject> _transformLookthroughObjects = new HashSet<GameObject>();

        internal static void Init(GameObject root)
        {
            _objectToOriginalPaths.Clear();
            _originalPathToMappedPath = null;
            _transformLookthroughObjects.Clear();

            foreach (var xform in root.GetComponentsInChildren<Transform>(true))
            {
                var path = RuntimeUtil.RelativePath(root, xform.gameObject);
                _objectToOriginalPaths.Add(xform.gameObject, new List<string> {path});
            }

            ClearCache();
        }

        internal static void ClearCache()
        {
            _originalPathToMappedPath = _transformOriginalPathToMappedPath = null;
        }

        /// <summary>
        /// Returns a path identifying a given object. This can include objects not originally present; in this case,
        /// they will be assigned a randomly-generated internal ID which will be replaced during path remapping with
        /// the true path.
        /// </summary>
        /// <param name="obj">Object to map</param>
        /// <returns></returns>
        internal static string GetObjectIdentifier(GameObject obj)
        {
            if (_objectToOriginalPaths.TryGetValue(obj, out var paths))
            {
                return paths[0];
            }
            else
            {
                var internalPath = "_ModularAvatarInternal/" + GUID.Generate();
                _objectToOriginalPaths.Add(obj, new List<string> {internalPath});
                return internalPath;
            }
        }

        /// <summary>
        /// When animating a transform component on a merged bone, we want to make sure we manipulate the original
        /// avatar's bone, not a stub bone attached underneath. By making an object as transform lookthrough, any
        /// queries for mapped paths on the transform component will walk up the tree to the next parent.
        /// </summary>
        /// <param name="obj">The object to mark transform lookthrough</param>
        internal static void MarkTransformLookthrough(GameObject obj)
        {
            ClearCache();
            _transformLookthroughObjects.Add(obj);
        }

        /// <summary>
        /// Marks an object as having been removed. Its paths will be remapped to its parent. 
        /// </summary>
        /// <param name="obj"></param>
        internal static void MarkRemoved(GameObject obj)
        {
            ClearCache();
            if (_objectToOriginalPaths.TryGetValue(obj, out var paths))
            {
                var parent = obj.transform.parent.gameObject;
                if (_objectToOriginalPaths.TryGetValue(parent, out var parentPaths))
                {
                    parentPaths.AddRange(paths);
                }

                _objectToOriginalPaths.Remove(obj);
                _transformLookthroughObjects.Remove(obj);
            }
        }

        private static ImmutableDictionary<string, string> BuildMapping(ref ImmutableDictionary<string, string> cache,
            bool transformLookup)
        {
            if (cache != null) return cache;

            ImmutableDictionary<string, string>.Builder builder = ImmutableDictionary.CreateBuilder<string, string>();

            foreach (var kvp in _objectToOriginalPaths)
            {
                var obj = kvp.Key;
                var paths = kvp.Value;

                if (transformLookup)
                {
                    while (_transformLookthroughObjects.Contains(obj))
                    {
                        obj = obj.transform.parent.gameObject;
                    }
                }

                var newPath = RuntimeUtil.AvatarRootPath(obj);
                foreach (var origPath in paths)
                {
                    builder.Add(origPath, newPath);
                }
            }

            cache = builder.ToImmutableDictionary();
            return cache;
        }

        internal static string MapPath(string path, bool isTransformMapping = false)
        {
            ImmutableDictionary<string, string> mappings;

            if (isTransformMapping)
            {
                mappings = BuildMapping(ref _originalPathToMappedPath, true);
            }
            else
            {
                mappings = BuildMapping(ref _transformOriginalPathToMappedPath, false);
            }

            if (mappings.TryGetValue(path, out var mappedPath))
            {
                return mappedPath;
            }
            else
            {
                return path;
            }
        }
    }
}