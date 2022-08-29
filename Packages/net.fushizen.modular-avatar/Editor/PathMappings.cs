using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    public static class PathMappings
    {
        private static SortedDictionary<string, string> Mappings = new SortedDictionary<string, string>();
        private static List<string> CachedMappingKeys = null;

        internal static void Clear()
        {
            Mappings.Clear();
            CachedMappingKeys = null;
        }

        internal static void Remap(string from, string to)
        {
            Mappings[from] = to;
            CachedMappingKeys = null;
        }

        internal static string MapPath(string path)
        {
            if (CachedMappingKeys == null) CachedMappingKeys = new List<string>(Mappings.Keys);
            var bsResult = CachedMappingKeys.BinarySearch(path);
            if (bsResult >= 0) return Mappings[path];

            int index = ~bsResult;
            if (index == 0) return path;

            var priorKey = CachedMappingKeys[index - 1];
            if (path.StartsWith(priorKey + "/"))
            {
                return Mappings[priorKey] + path.Substring(priorKey.Length);
            }
            return path;
        }
    }

    internal class ClearPathMappings : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => HookSequence.SEQ_RESETTERS;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            PathMappings.Clear();
            return true;
        }
    }
}