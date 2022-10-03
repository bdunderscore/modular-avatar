using System;
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    [Serializable]
    public class AvatarObjectReference
    {
        public bool isNull = true;
        public string referencePath = "";

        private bool _cacheValid = false;
        private string _cachedPath = "";
        private GameObject _cachedReference;

        public GameObject Get(Component container)
        {
            if (_cacheValid && _cachedPath == referencePath && !isNull) return _cachedReference;

            _cacheValid = true;
            _cachedPath = referencePath;

            if (isNull)
            {
                _cachedReference = null;
                return _cachedReference;
            }

            RuntimeUtil.OnHierarchyChanged -= InvalidateCache;
            RuntimeUtil.OnHierarchyChanged += InvalidateCache;

            var avatar = RuntimeUtil.FindAvatarInParents(container.transform);
            if (avatar == null) return (_cachedReference = null);

            return (_cachedReference = avatar.transform.Find(referencePath)?.gameObject);
        }

        private void InvalidateCache()
        {
            RuntimeUtil.OnHierarchyChanged -= InvalidateCache;
            _cacheValid = false;
        }
    }
}