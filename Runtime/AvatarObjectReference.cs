using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public class AvatarObjectReference
    {
        private static long HIERARCHY_CHANGED_SEQ = long.MinValue;
        private long ReferencesLockedAtFrame = long.MinValue;

        public static string AVATAR_ROOT = "$$$AVATAR_ROOT$$$";
        public static string CONTAINER_ROOT = "$$$CONTAINER_ROOT$$$";

        public enum PathMode
        {
            Absolute,
            Relative
        }

        public PathMode pathMode = PathMode.Absolute;
        public string referencePath;
        public string relativeReferencePath;

        [SerializeField] internal GameObject targetObject;

        private long _cacheSeq = long.MinValue;
        private bool _cacheValid;
        private PathMode _cachedMode;
        private string _cachedPath;
        private GameObject _cachedReference;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.hierarchyChanged += () => HIERARCHY_CHANGED_SEQ += 1;
        }
#endif

        public AvatarObjectReference()
        {
        }

        public AvatarObjectReference(GameObject obj)
        {
            Set(obj);
        }

        internal static void InvalidateAll()
        {
            HIERARCHY_CHANGED_SEQ++;
        }
        
        public AvatarObjectReference Clone()
        {
            return new AvatarObjectReference
            {
                pathMode = pathMode,
                referencePath = referencePath,
                relativeReferencePath = relativeReferencePath,
                targetObject = targetObject
            };
        }
            
        #if UNITY_EDITOR
        public static GameObject Get(SerializedProperty prop)
        {
            var rootObject = prop.serializedObject.targetObject;
            if (rootObject == null) return null;
            
            var containerTransform = (rootObject as Component)?.transform ?? (rootObject as GameObject)?.transform;
            if (containerTransform == null) return null;

            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(containerTransform);
            if (avatarRoot == null) return null;
            
            var pathMode = (PathMode)prop.FindPropertyRelative("pathMode").enumValueIndex;
            var referencePath = prop.FindPropertyRelative("referencePath").stringValue;
            var relativeReferencePath = prop.FindPropertyRelative("relativeReferencePath").stringValue;
            var targetObject = prop.FindPropertyRelative("targetObject").objectReferenceValue as GameObject;

            if (pathMode == PathMode.Absolute)
            {
                if (targetObject != null && targetObject.transform.IsChildOf(avatarRoot))
                    return targetObject;

                if (referencePath == AVATAR_ROOT)
                    return avatarRoot.gameObject;

                return avatarRoot.Find(referencePath)?.gameObject;
            }
            else if (pathMode == PathMode.Relative)
            {
                if (targetObject != null && (targetObject.transform == containerTransform || targetObject.transform.IsChildOf(containerTransform)))
                    return targetObject;

                if (relativeReferencePath == CONTAINER_ROOT)
                    return containerTransform.gameObject;

                return RuntimeUtil.ResolveParentAllowedRelativePath(containerTransform, relativeReferencePath, avatarRoot)?.gameObject;
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }
        #endif
        
        public GameObject Get(Component container)
        {
            bool cacheValid = _cacheValid || ReferencesLockedAtFrame == Time.frameCount;
            cacheValid &= HIERARCHY_CHANGED_SEQ == _cacheSeq;
            cacheValid &= _cachedMode == pathMode;
            
            var path = pathMode switch
            {
                PathMode.Absolute => referencePath,
                PathMode.Relative => relativeReferencePath,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (cacheValid && _cachedPath == path && _cachedReference != null) return _cachedReference;

            _cacheValid = true;
            _cacheSeq = HIERARCHY_CHANGED_SEQ;
            _cachedMode = pathMode;
            _cachedPath = path;

            if (string.IsNullOrEmpty(path))
            {
                _cachedReference = null;
                return _cachedReference;
            }

            var avatarTransform = RuntimeUtil.FindAvatarTransformInParents(container.transform);
            if (avatarTransform == null) return (_cachedReference = null);

            if (pathMode == PathMode.Absolute)
            {
                if (targetObject != null && targetObject.transform.IsChildOf(avatarTransform))
                    return _cachedReference = targetObject;

                if (referencePath == AVATAR_ROOT)
                    return _cachedReference = avatarTransform.gameObject;

                _cachedReference = avatarTransform.Find(referencePath)?.gameObject;
            }
            else if (pathMode == PathMode.Relative)
            {
                if (targetObject != null && (targetObject.transform == container.transform || targetObject.transform.IsChildOf(container.transform)))
                    return _cachedReference = targetObject;

                if (path == CONTAINER_ROOT)
                    return _cachedReference = container.gameObject;

                _cachedReference = RuntimeUtil.ResolveParentAllowedRelativePath(container.transform, path, avatarTransform)?.gameObject;
            }

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
            pathMode = PathMode.Absolute;
            relativeReferencePath = "";

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
            targetObject = target;
        }

        public void SetRelative(Component container, GameObject target)
        {
            pathMode = PathMode.Relative;
            referencePath = "";

            if (container == null || target == null)
            {
                relativeReferencePath = "";
            }
            else if (target.transform == container.transform)
            {
                relativeReferencePath = CONTAINER_ROOT;
            }
            else
            {
                var avatarTransform = RuntimeUtil.FindAvatarTransformInParents(container.transform);
                var path = RuntimeUtil.ParentAllowedRelativePath(container.transform, target.transform, avatarTransform);
                relativeReferencePath = string.IsNullOrEmpty(path) ? "" : path;
            }

            _cachedReference = target;
            _cacheValid = true;
            targetObject = target;
        }

       internal bool IsConsistent(GameObject avatarRoot, Component container)
        {
            if (pathMode == PathMode.Absolute)
            {
                if (referencePath == AVATAR_ROOT) return targetObject == avatarRoot;
                if (avatarRoot.transform.Find(referencePath)?.gameObject == targetObject)
                {
                    return true;
                }

                // If multiple objects match the same path, then we accept that the reference is consistent.
                var targetObjectPath = RuntimeUtil.AvatarRootPath(targetObject);
                return targetObjectPath == referencePath;
            }
            else if (pathMode == PathMode.Relative)
            {
                if (relativeReferencePath == CONTAINER_ROOT) return targetObject == container.gameObject;
                if (RuntimeUtil.ResolveParentAllowedRelativePath(container.transform, relativeReferencePath, avatarRoot.transform)?.gameObject == targetObject)
                {
                    return true;
                }

                // If multiple objects match the same path, then we accept that the reference is consistent.
                var targetObjectPath = RuntimeUtil.ParentAllowedRelativePath(container.transform, targetObject.transform, avatarRoot.transform);
                return targetObjectPath == relativeReferencePath;
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }
        
        protected bool Equals(AvatarObjectReference other)
        {
            if (GetDirectTarget() != other.GetDirectTarget() || pathMode != other.pathMode) return false;

            if (pathMode == PathMode.Absolute)
            {
                return referencePath == other.referencePath;
            }
            else if (pathMode == PathMode.Relative)
            {
                return relativeReferencePath == other.relativeReferencePath;
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        private GameObject GetDirectTarget()
        {
            return targetObject != null ? targetObject : null;
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
            var hashCode = (int)pathMode;
            if (pathMode == PathMode.Absolute)
            {
                hashCode = (hashCode * 397) ^ (referencePath != null ? referencePath.GetHashCode() : 0);
            }
            else if (pathMode == PathMode.Relative)
            {
                hashCode = (hashCode * 397) ^ (relativeReferencePath != null ? relativeReferencePath.GetHashCode() : 0);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
            return hashCode;
        }
    }
}