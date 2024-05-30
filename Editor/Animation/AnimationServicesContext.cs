#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.animation
{
    /// <summary>
    /// This extension context amortizes a number of animation-related processing steps - notably,
    /// collecting the set of all animation clips from the animators, and committing changes to them
    /// in a deferred manner.
    ///
    /// Restrictions: While this context is active, any changes to clips must be done by editing them via
    /// ClipHolders in the AnimationDatabase. Any newly added clips must be registered in the AnimationDatabase,
    /// and any new references to clips require setting appropriate ClipCommitActions.
    ///
    /// New references to objects created in clips must use paths obtained from the
    /// ObjectRenameTracker.GetObjectIdentifier method.
    /// </summary>
    internal sealed class AnimationServicesContext : IExtensionContext
    {
        private BuildContext _context;
        private AnimationDatabase _animationDatabase;
        private PathMappings _pathMappings;
        private Dictionary<GameObject, string> _selfProxies = new();

        public void OnActivate(BuildContext context)
        {
            _context = context;
            
            _animationDatabase = new AnimationDatabase();
            _animationDatabase.OnActivate(context);

            _pathMappings = new PathMappings();
            _pathMappings.OnActivate(context, _animationDatabase);
        }

        public void OnDeactivate(BuildContext context)
        {
            _pathMappings.OnDeactivate(context);
            _animationDatabase.Commit();

            _pathMappings = null;
            _animationDatabase = null;
        }

        public AnimationDatabase AnimationDatabase
        {
            get
            {
                if (_animationDatabase == null)
                {
                    throw new InvalidOperationException(
                        "AnimationDatabase is not available outside of the AnimationServicesContext");
                }

                return _animationDatabase;
            }
        }

        public PathMappings PathMappings
        {
            get
            {
                if (_pathMappings == null)
                {
                    throw new InvalidOperationException(
                        "ObjectRenameTracker is not available outside of the AnimationServicesContext");
                }

                return _pathMappings;
            }
        }

        /// <summary>
        /// Returns a parameter which proxies the "activeSelf" state of the specified GameObject.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool TryGetActiveSelfProxy(GameObject obj, out string paramName)
        {
            if (_selfProxies.TryGetValue(obj, out paramName)) return !string.IsNullOrEmpty(paramName);

            var path = PathMappings.GetObjectIdentifier(obj);
            var clips = AnimationDatabase.ClipsForPath(path);
            if (clips == null || clips.IsEmpty)
            {
                _selfProxies[obj] = "";
                return false;
            }

            var iid = obj.GetInstanceID();
            paramName = $"_MA/ActiveSelf/{iid}";

            var binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");

            bool hadAnyClip = false;
            foreach (var clip in clips)
            {
                Motion newMotion = ProcessActiveSelf(clip.CurrentClip, paramName, binding);
                if (newMotion != clip.CurrentClip)
                {
                    clip.SetCurrentNoInvalidate(newMotion);
                    hadAnyClip = true;
                }
            }

            if (hadAnyClip)
            {
                _selfProxies[obj] = paramName;
                return true;
            }
            else
            {
                _selfProxies[obj] = "";
                return false;
            }
        }

        private Motion ProcessActiveSelf(Motion motion, string paramName, EditorCurveBinding binding)
        {
            if (motion is AnimationClip clip)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) return motion;

                var newClip = new AnimationClip();
                EditorUtility.CopySerialized(motion, newClip);

                newClip.SetCurve("", typeof(Animator), paramName, curve);
                return newClip;
            }
            else if (motion is BlendTree bt)
            {
                bool anyChanged = false;

                var motions = bt.children.Select(c => // c is struct ChildMotion
                {
                    var newMotion = ProcessActiveSelf(c.motion, paramName, binding);
                    anyChanged |= newMotion != c.motion;
                    c.motion = newMotion;
                    return c;
                }).ToArray();

                if (anyChanged)
                {
                    var newBt = new BlendTree();
                    EditorUtility.CopySerialized(bt, newBt);

                    newBt.children = motions;
                    return newBt;
                }
                else
                {
                    return bt;
                }
            }
            else
            {
                return motion;
            }
        }
    }
}