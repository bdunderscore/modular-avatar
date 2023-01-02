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

using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace nadena.dev.modular_avatar.core.editor
{
    // TODO - needs optimization pass maybe?
    internal static class PathMappings
    {
        private static List<(string, MappingEntry)> Mappings = new List<(string, MappingEntry)>();
        private static Dictionary<(bool, string), string> MappingCache = new Dictionary<(bool, string), string>();

        internal struct MappingEntry
        {
            public string path;
            public string transformPath;

            public string Get(bool isTransformMapping)
            {
                return isTransformMapping ? transformPath : path;
            }
        }

        internal static void Clear()
        {
            Mappings.Clear();
            MappingCache.Clear();
        }

        internal static void Remap(string from, MappingEntry to)
        {
            Mappings.Add((from, to));
            MappingCache.Clear();
        }

        internal static string MapPath(string path, bool isTransformMapping = false)
        {
            var cacheKey = (isTransformMapping, path);
            if (MappingCache.TryGetValue(cacheKey, out var result)) return result;

            if (path.Contains("ToggleTest"))
            {
                MappingCache.Clear();
            }

            foreach (var (src, mapping) in Mappings)
            {
                if (path == src || path.StartsWith(src + "/"))
                {
                    var suffix = path.Substring(src.Length);
                    path = mapping.Get(isTransformMapping) + suffix;

                    // Continue processing subsequent remappings
                }
            }

            MappingCache[cacheKey] = path;

            return path;
        }
    }
}