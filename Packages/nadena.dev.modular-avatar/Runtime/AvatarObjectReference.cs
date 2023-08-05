using System;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public class AvatarObjectReference
    {
        private long ReferencesLockedAtFrame = long.MinValue;

        public static string AVATAR_ROOT = "$$$AVATAR_ROOT$$$";
        public string referencePath;

        private bool _cacheValid;
        private string _cachedPath;
        private GameObject _cachedReference;

        public GameObject Get(Component container)
        {
            bool cacheValid = _cacheValid || ReferencesLockedAtFrame == Time.frameCount;

            if (cacheValid && _cachedPath == referencePath && _cachedReference != null) return _cachedReference;

            _cacheValid = true;
            _cachedPath = referencePath;

            if (string.IsNullOrEmpty(referencePath))
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

        public void Set(GameObject target)
        {
            if (target == null)
            {
                referencePath = "";
            }
            else if (target.GetComponent<VRCAvatarDescriptor>() != null)
            {
                referencePath = AVATAR_ROOT;
            }
            else
            {
                referencePath = RuntimeUtil.AvatarRootPath(target);
            }

            _cachedReference = target;
            _cacheValid = true;
        }

        private void InvalidateCache()
        {
            RuntimeUtil.OnHierarchyChanged -= InvalidateCache;
            _cacheValid = false;
        }

        protected bool Equals(AvatarObjectReference other)
        {
            return referencePath == other.referencePath;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AvatarObjectReference) obj);
        }

        public override int GetHashCode()
        {
            return (referencePath != null ? referencePath.GetHashCode() : 0);
        }
    }
}