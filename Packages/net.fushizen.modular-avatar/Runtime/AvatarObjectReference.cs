using System;
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    [Serializable]
    public struct AvatarObjectReference
    {
        public static string AVATAR_ROOT = "$$$AVATAR_ROOT$$$";
        public string referencePath;

        private bool _cacheValid;
        private string _cachedPath;
        private GameObject _cachedReference;

        public GameObject Get(Component container)
        {
            if (_cacheValid && _cachedPath == referencePath) return _cachedReference;

            _cacheValid = true;
            _cachedPath = referencePath;

            if (referencePath == "")
            {
                _cachedReference = null;
                return _cachedReference;
            }

            RuntimeUtil.OnHierarchyChanged -= InvalidateCache;
            RuntimeUtil.OnHierarchyChanged += InvalidateCache;

            var avatar = RuntimeUtil.FindAvatarInParents(container.transform);
            if (avatar == null) return (_cachedReference = null);

            if (referencePath == AVATAR_ROOT)
            {
                _cachedReference = avatar.gameObject;
                return _cachedReference;
            }

            return (_cachedReference = avatar.transform.Find(referencePath)?.gameObject);
        }

        private void InvalidateCache()
        {
            RuntimeUtil.OnHierarchyChanged -= InvalidateCache;
            _cacheValid = false;
        }
    }
}