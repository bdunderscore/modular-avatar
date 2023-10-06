using System;
using UnityEngine;

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

            _cachedReference = avatar.transform.Find(referencePath)?.gameObject;
            if (_cachedReference == null) return null;
            
            // https://github.com/bdunderscore/modular-avatar/issues/308
            // Some avatars have multiple "Armature" objects in order to confuse VRChat into changing the avatar eye
            // position. We need to be smarter than VRChat and find the "true" armature in this case.
            var targetName = _cachedReference.name;
            var parent = _cachedReference.transform.parent;
            if (targetName == "Armature" && parent != null && _cachedReference.transform.childCount == 0)
            {
                foreach (Transform possibleTarget in parent)
                {
                    if (possibleTarget.gameObject.name == targetName && possibleTarget.childCount > 0)
                    {
                        _cachedReference = possibleTarget.gameObject;
                        break;
                    }
                }
            }

            return _cachedReference;
        }

        public void Set(GameObject target)
        {
            if (target == null)
            {
                referencePath = "";
            }
            else if (RuntimeUtil.IsAvatarRoot(target.transform))
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