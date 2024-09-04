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
        public string referencePath;

        [SerializeField] internal GameObject targetObject;

        private long _cacheSeq = long.MinValue;
        private bool _cacheValid;
        private string _cachedPath;
        private GameObject _cachedReference;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.hierarchyChanged += () => HIERARCHY_CHANGED_SEQ += 1;
        }
#endif
        
        public AvatarObjectReference Clone()
        {
            return new AvatarObjectReference
            {
                referencePath = referencePath,
                targetObject = targetObject
            };
        }
            
        #if UNITY_EDITOR
        public static GameObject Get(SerializedProperty prop)
        {
            var rootObject = prop.serializedObject.targetObject;
            if (rootObject == null) return null;
            
            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents((rootObject as Component)?.transform ?? (rootObject as GameObject)?.transform);
            if (avatarRoot == null) return null;
            
            var referencePath = prop.FindPropertyRelative("referencePath").stringValue;
            var targetObject = prop.FindPropertyRelative("targetObject").objectReferenceValue as GameObject;
            
            if (targetObject != null && targetObject.transform.IsChildOf(avatarRoot))
                return targetObject;
            
            if (referencePath == AVATAR_ROOT)
                return avatarRoot.gameObject;
            
            return avatarRoot.Find(referencePath)?.gameObject;
        }
        #endif
        
        public GameObject Get(Component container)
        {
            bool cacheValid = _cacheValid || ReferencesLockedAtFrame == Time.frameCount;
            cacheValid &= HIERARCHY_CHANGED_SEQ == _cacheSeq;
            
            if (cacheValid && _cachedPath == referencePath && _cachedReference != null) return _cachedReference;

            _cacheValid = true;
            _cacheSeq = HIERARCHY_CHANGED_SEQ;
            _cachedPath = referencePath;

            if (string.IsNullOrEmpty(referencePath))
            {
                _cachedReference = null;
                return _cachedReference;
            }

            var avatarTransform = RuntimeUtil.FindAvatarTransformInParents(container.transform);
            if (avatarTransform == null) return (_cachedReference = null);

            if (targetObject != null && targetObject.transform.IsChildOf(avatarTransform))
                return _cachedReference = targetObject;

            if (referencePath == AVATAR_ROOT)
            {
                _cachedReference = avatarTransform.gameObject;
                return _cachedReference;
            }

            _cachedReference = avatarTransform.Find(referencePath)?.gameObject;
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
            targetObject = target;
        }

        internal bool IsConsistent(GameObject avatarRoot)
        {
            if (referencePath == AVATAR_ROOT) return targetObject == avatarRoot;
            return avatarRoot.transform.Find(referencePath)?.gameObject == targetObject;
        }
        
        protected bool Equals(AvatarObjectReference other)
        {
            return GetDirectTarget() == other.GetDirectTarget() && referencePath == other.referencePath;
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
            return (referencePath != null ? referencePath.GetHashCode() : 0);
        }
    }
}