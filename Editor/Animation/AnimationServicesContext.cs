#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

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
        private ReadableProperty _readableProperty;
        
        private Dictionary<GameObject, string> _selfProxies = new();

        public void OnActivate(BuildContext context)
        {
            _context = context;
            
            _animationDatabase = new AnimationDatabase();
            _animationDatabase.OnActivate(context);

            _pathMappings = new PathMappings();
            _pathMappings.OnActivate(context, _animationDatabase);

            _readableProperty = new ReadableProperty(_context, _animationDatabase, this);
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

        public IEnumerable<(EditorCurveBinding, string)> BoundReadableProperties => _readableProperty.BoundProperties;

        // HACK: This is a temporary crutch until we rework the entire animator services system
        public void AddPropertyDefinition(AnimatorControllerParameter paramDef)
        {
            var fx = (AnimatorController)
                _context.AvatarDescriptor.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController;

            fx.parameters = fx.parameters.Concat(new[] { paramDef }).ToArray();
        }

        public string GetActiveSelfProxy(GameObject obj)
        {
            if (_selfProxies.TryGetValue(obj, out var paramName) && !string.IsNullOrEmpty(paramName)) return paramName;

            var path = PathMappings.GetObjectIdentifier(obj);

            paramName = _readableProperty.ForActiveSelf(path);
            _selfProxies[obj] = paramName;

            return paramName;
        }

        public bool ObjectHasAnimations(GameObject obj)
        {
            var path = PathMappings.GetObjectIdentifier(obj);
            var clips = AnimationDatabase.ClipsForPath(path);
            return clips != null && !clips.IsEmpty;
        }
    }
}