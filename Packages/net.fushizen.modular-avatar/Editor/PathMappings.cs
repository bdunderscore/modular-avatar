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
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    public static class PathMappings
    {
        private static SortedDictionary<string, MappingEntry> Mappings = new SortedDictionary<string, MappingEntry>();
        private static List<string> CachedMappingKeys = null;

        public struct MappingEntry
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
            CachedMappingKeys = null;
        }

        internal static void Remap(string from, MappingEntry to)
        {
            Mappings[from] = to;
            CachedMappingKeys = null;
        }

        internal static string MapPath(string path, bool isTransformMapping = false)
        {
            if (CachedMappingKeys == null) CachedMappingKeys = new List<string>(Mappings.Keys);
            var bsResult = CachedMappingKeys.BinarySearch(path);
            if (bsResult >= 0) return Mappings[path].Get(isTransformMapping);

            int index = ~bsResult;
            if (index == 0) return path;

            var priorKey = CachedMappingKeys[index - 1];
            if (path.StartsWith(priorKey + "/"))
            {
                return Mappings[priorKey].Get(isTransformMapping) + path.Substring(priorKey.Length);
            }
            return path;
        }
    }
}