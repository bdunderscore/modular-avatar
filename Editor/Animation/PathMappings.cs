#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.util;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Profiling;
#if MA_VRCSDK3_AVATARS_3_5_2_OR_NEWER
#endif

#endregion

namespace nadena.dev.modular_avatar.animation
{
    #region

    #endregion

    /// <summary>
    /// This extension context tracks when objects are renamed, and updates animations accordingly.
    /// Users of this context need to be aware that, when creating new curves (or otherwise introducing new motions,
    /// use context.ObjectPath to obtain a suitable path for the target objects).
    /// </summary>
    internal sealed class PathMappings
    {
        private AnimationDatabase _animationDatabase;

        private Dictionary<GameObject, List<string>>
            _objectToOriginalPaths = new Dictionary<GameObject, List<string>>();

        private HashSet<GameObject> _transformLookthroughObjects = new HashSet<GameObject>();
        private ImmutableDictionary<string, string> _originalPathToMappedPath = null;
        private ImmutableDictionary<string, string> _transformOriginalPathToMappedPath = null;
        private ImmutableDictionary<string, GameObject> _pathToObject = null;

        internal void OnActivate(BuildContext context, AnimationDatabase animationDatabase)
        {
            _animationDatabase = animationDatabase;
            _objectToOriginalPaths.Clear();
            _transformLookthroughObjects.Clear();
            ClearCache();

            foreach (var xform in context.AvatarRootTransform.GetComponentsInChildren<Transform>(true))
            {
                _objectToOriginalPaths.Add(xform.gameObject, new List<string> {xform.gameObject.AvatarRootPath()});
            }
        }

        public void ClearCache()
        {
            _originalPathToMappedPath = null;
            _transformOriginalPathToMappedPath = null;
            _pathToObject = null;
        }

        /// <summary>
        /// Sets the "transform lookthrough" flag for an object. Any transform animations on this object will be
        /// redirected to its parent. This is used in Modular Avatar as part of bone merging logic.
        /// </summary>
        /// <param name="obj"></param>
        public void MarkTransformLookthrough(GameObject obj)
        {
            _transformLookthroughObjects.Add(obj);
        }

        /// <summary>
        /// Returns a path for use in dynamically generated animations for a given object. This can include objects not
        /// present at the time of context activation; in this case, they will be assigned a randomly-generated internal
        /// path and replaced during path remapping with the true path.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public string GetObjectIdentifier(GameObject obj)
        {
            if (_objectToOriginalPaths.TryGetValue(obj, out var paths))
            {
                return paths[0];
            }
            else
            {
                var internalPath = "_NewlyCreatedObject/" + GUID.Generate() + "/" + obj.AvatarRootPath();
                _objectToOriginalPaths.Add(obj, new List<string> {internalPath});
                return internalPath;
            }
        }

        /// <summary>
        /// Marks an object as having been removed. Its paths will be remapped to its parent. 
        /// </summary>
        /// <param name="obj"></param>
        public void MarkRemoved(GameObject obj)
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


        /// <summary>
        /// Marks an object as having been replaced by another object. All references to the old object will be replaced
        /// by the new object. References originally to the new object will continue to point to the new object.
        /// </summary>
        /// <param name="old"></param>
        /// <param name="newObject"></param>
        public void ReplaceObject(GameObject old, GameObject newObject)
        {
            ClearCache();

            if (_objectToOriginalPaths.TryGetValue(old, out var paths))
            {
                if (!_objectToOriginalPaths.TryGetValue(newObject, out var newObjectPaths))
                {
                    newObjectPaths = new List<string>();
                    _objectToOriginalPaths.Add(newObject, newObjectPaths);
                }

                newObjectPaths.AddRange(paths);

                _objectToOriginalPaths.Remove(old);
            }


            if (_transformLookthroughObjects.Contains(old))
            {
                _transformLookthroughObjects.Remove(old);
                _transformLookthroughObjects.Add(newObject);
            }
        }


        private ImmutableDictionary<string, string> BuildMapping(ref ImmutableDictionary<string, string> cache,
            bool transformLookup)
        {
            if (cache != null) return cache;

            ImmutableDictionary<string, string> dict = ImmutableDictionary<string, string>.Empty;

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

                var newPath = obj.AvatarRootPath();
                foreach (var origPath in paths)
                {
                    if (!dict.ContainsKey(origPath))
                    {
                        dict = dict.Add(origPath, newPath);
                    }
                }
            }

            cache = dict;
            return cache;
        }

        public string MapPath(string path, bool isTransformMapping = false)
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

        private string MapPath(EditorCurveBinding binding)
        {
            if (binding.type == typeof(Animator) && binding.path == "")
            {
                return "";
            }
            else
            {
                return MapPath(binding.path, binding.type == typeof(Transform));
            }
        }

        private AnimationClip ApplyMappingsToClip(AnimationClip originalClip,
            Dictionary<AnimationClip, AnimationClip> clipCache)
        {
            if (originalClip == null) return null;
            if (clipCache != null && clipCache.TryGetValue(originalClip, out var cachedClip)) return cachedClip;

            if (originalClip.IsProxyAnimation()) return originalClip;

            var curveBindings = AnimationUtility.GetCurveBindings(originalClip);
            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(originalClip);

            bool hasMapping = false;
            foreach (var binding in curveBindings.Concat(objectBindings))
            {
                if (MapPath(binding) != binding.path)
                {
                    hasMapping = true;
                    break;
                }
            }

            if (!hasMapping) return originalClip;


            var newClip = new AnimationClip();
            newClip.name = originalClip.name;

            SerializedObject before = new SerializedObject(originalClip);
            SerializedObject after = new SerializedObject(newClip);

            var before_hqCurve = before.FindProperty("m_UseHighQualityCurve");
            var after_hqCurve = after.FindProperty("m_UseHighQualityCurve");

            after_hqCurve.boolValue = before_hqCurve.boolValue;
            after.ApplyModifiedPropertiesWithoutUndo();

            // TODO - should we use direct SerializedObject manipulation to avoid missing script issues?
            foreach (var binding in curveBindings)
            {
                var newBinding = binding;
                newBinding.path = MapPath(binding);
                // https://github.com/bdunderscore/modular-avatar/issues/950
                // It's reported that sometimes using SetObjectReferenceCurve right after SetCurve might cause the
                // curves to be forgotten; use SetEditorCurve instead.
                AnimationUtility.SetEditorCurve(newClip, newBinding,
                    AnimationUtility.GetEditorCurve(originalClip, binding));
            }

            foreach (var objBinding in objectBindings)
            {
                var newBinding = objBinding;
                newBinding.path = MapPath(objBinding);
                AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                    AnimationUtility.GetObjectReferenceCurve(originalClip, objBinding));
            }

            newClip.wrapMode = originalClip.wrapMode;
            newClip.legacy = originalClip.legacy;
            newClip.frameRate = originalClip.frameRate;
            newClip.localBounds = originalClip.localBounds;
            AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(originalClip));

            if (clipCache != null)
            {
                clipCache.Add(originalClip, newClip);
            }

            return newClip;
        }

        private void ApplyMappingsToAvatarMask(AvatarMask mask)
        {
            if (mask == null) return;

            var maskSo = new SerializedObject(mask);

            var seenTransforms = new Dictionary<string, float>();
            var transformOrder = new List<string>();
            var m_Elements = maskSo.FindProperty("m_Elements");
            var elementCount = m_Elements.arraySize;

            for (var i = 0; i < elementCount; i++)
            {
                var element = m_Elements.GetArrayElementAtIndex(i);
                var path = element.FindPropertyRelative("m_Path").stringValue;
                var weight = element.FindPropertyRelative("m_Weight").floatValue;

                path = MapPath(path);

                // ensure all parent elements are present
                EnsureParentsPresent(path);

                if (!seenTransforms.ContainsKey(path)) transformOrder.Add(path);
                seenTransforms[path] = weight;
            }

            transformOrder.Sort();
            m_Elements.arraySize = transformOrder.Count;

            for (var i = 0; i < transformOrder.Count; i++)
            {
                var element = m_Elements.GetArrayElementAtIndex(i);
                var path = transformOrder[i];

                element.FindPropertyRelative("m_Path").stringValue = path;
                element.FindPropertyRelative("m_Weight").floatValue = seenTransforms[path];
            }

            maskSo.ApplyModifiedPropertiesWithoutUndo();

            void EnsureParentsPresent(string path)
            {
                var nextSlash = -1;

                while ((nextSlash = path.IndexOf('/', nextSlash + 1)) != -1)
                {
                    var parentPath = path.Substring(0, nextSlash);
                    if (!seenTransforms.ContainsKey(parentPath))
                    {
                        seenTransforms[parentPath] = 0;
                        transformOrder.Add(parentPath);
                    }
                }
            }
        }

        internal void OnDeactivate(BuildContext context)
        {
            Profiler.BeginSample("PathMappings.OnDeactivate");
            Dictionary<AnimationClip, AnimationClip> clipCache = new Dictionary<AnimationClip, AnimationClip>();
            
            Profiler.BeginSample("ApplyMappingsToClip");
            _animationDatabase.ForeachClip(holder =>
            {
                if (holder.CurrentClip is AnimationClip clip)
                {
                    holder.CurrentClip = ApplyMappingsToClip(clip, clipCache);
                }
            });
            Profiler.EndSample();

#if MA_VRCSDK3_AVATARS_3_5_2_OR_NEWER
            Profiler.BeginSample("MapPlayAudio");
            _animationDatabase.ForeachPlayAudio(playAudio =>
            {
                if (playAudio == null) return;
                playAudio.SourcePath = MapPath(playAudio.SourcePath, true);
            });
            Profiler.EndSample();
#endif

            Profiler.BeginSample("InvokeIOnCommitObjectRenamesCallbacks");
            foreach (var listener in context.AvatarRootObject.GetComponentsInChildren<IOnCommitObjectRenames>())
            {
                listener.OnCommitObjectRenames(context, this);
            }
            Profiler.EndSample();

            var layers = context.AvatarDescriptor.baseAnimationLayers
                .Concat(context.AvatarDescriptor.specialAnimationLayers);

            Profiler.BeginSample("ApplyMappingsToAvatarMasks");
            foreach (var layer in layers)
            {
                ApplyMappingsToAvatarMask(layer.mask);

                if (layer.animatorController is AnimatorController ac)
                    // By this point, all AnimationOverrideControllers have been collapsed into an ephemeral
                    // AnimatorController so we can safely modify the controller in-place.
                    foreach (var acLayer in ac.layers)
                        ApplyMappingsToAvatarMask(acLayer.avatarMask);
            }
            Profiler.EndSample();
            
            Profiler.EndSample();
        }

        public GameObject PathToObject(string path)
        {
            if (_pathToObject == null)
            {
                var builder = ImmutableDictionary.CreateBuilder<string, GameObject>();

                foreach (var kvp in _objectToOriginalPaths)
                foreach (var p in kvp.Value)
                    builder[p] = kvp.Key;

                _pathToObject = builder.ToImmutable();
            }
            
            if (_pathToObject.TryGetValue(path, out var obj))
            {
                return obj;
            }
            else
            {
                return null;
            }
        }
    }
}