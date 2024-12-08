#region

using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
#endif

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
    }
}