using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Odbc;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.animation
{
    /// <summary>
    /// The animation database records the set of all clips which are used in the avatar, and which paths they
    /// manipulate.
    /// </summary>
    internal class AnimationDatabase
    {
        internal class ClipHolder
        {
            private readonly AnimationDatabase ParentDatabase;

            private Motion _currentClip;

            internal Motion CurrentClip
            {
                get
                {
                    ParentDatabase.InvalidateCaches();
                    return _currentClip;
                }
                set
                {
                    ParentDatabase.InvalidateCaches();
                    _currentClip = value;
                }
            }

            internal Motion OriginalClip { get; }
            internal readonly bool IsProxyAnimation;

            internal ClipHolder(AnimationDatabase parentDatabase, Motion clip)
            {
                ParentDatabase = parentDatabase;
                CurrentClip = OriginalClip = clip;
                IsProxyAnimation = clip != null && Util.IsProxyAnimation(clip);
            }

            /// <summary>
            /// Returns the current clip without invalidating caches. Do not modify this clip without taking extra
            /// steps to invalidate caches on the AnimationDatabase.
            /// </summary>
            /// <returns></returns>
            internal Motion GetCurrentClipUnsafe()
            {
                return _currentClip;
            }
        }

        private ndmf.BuildContext _context;

        private List<Action> _clipCommitActions = new List<Action>();
        private List<ClipHolder> _clips = new List<ClipHolder>();

        private Dictionary<string, HashSet<ClipHolder>> _pathToClip = null;

        internal AnimationDatabase()
        {
            Debug.Log("Creating animation database");
        }

        internal void Commit()
        {
            foreach (var clip in _clips)
            {
                if (clip.IsProxyAnimation) clip.CurrentClip = clip.OriginalClip;
            }

            foreach (var clip in _clips)
            {
                // Changing the "high quality curve" setting can result in behavior changes (but can happen accidentally
                // as we manipulate curves)
                if (clip.CurrentClip != clip.OriginalClip && clip.CurrentClip != null && clip.OriginalClip != null)
                {
                    SerializedObject before = new SerializedObject(clip.OriginalClip);
                    SerializedObject after = new SerializedObject(clip.CurrentClip);

                    var before_prop = before.FindProperty("m_UseHighQualityCurve");
                    var after_prop = after.FindProperty("m_UseHighQualityCurve");

                    if (after_prop.boolValue != before_prop.boolValue)
                    {
                        after_prop.boolValue = before_prop.boolValue;
                        after.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            foreach (var action in _clipCommitActions)
            {
                action();
            }
        }

        internal void OnActivate(ndmf.BuildContext context)
        {
            _context = context;

            AnimationUtil.CloneAllControllers(context);

#if MA_VRCSDK3_AVATARS
            var avatarDescriptor = context.AvatarDescriptor;

            foreach (var layer in avatarDescriptor.baseAnimationLayers)
            {
                BootstrapLayer(layer);
            }

            foreach (var layer in avatarDescriptor.specialAnimationLayers)
            {
                BootstrapLayer(layer);
            }

            void BootstrapLayer(VRCAvatarDescriptor.CustomAnimLayer layer)
            {
                if (!layer.isDefault && layer.animatorController is AnimatorController ac &&
                    context.IsTemporaryAsset(ac))
                {
                    BuildReport.ReportingObject(ac, () =>
                    {
                        foreach (var state in Util.States(ac))
                        {
                            RegisterState(state);
                        }
                    });
                }
            }
#endif
        }

        /// <summary>
        /// Registers a motion and all its reachable submotions with the animation database. The processClip callback,
        /// if provided, will be invoked for each newly discovered clip.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="processClip"></param>
        /// <exception cref="Exception"></exception>
        internal void RegisterState(AnimatorState state, Action<ClipHolder> processClip = null)
        {
            Dictionary<Motion, ClipHolder> _originalToHolder = new Dictionary<Motion, ClipHolder>();

            if (processClip == null) processClip = (_) => { };
            var isProxyAnim = Util.IsProxyAnimation(state.motion);

            if (state.motion == null) return;

            var clipHolder = RegisterMotion(state.motion, state, processClip, _originalToHolder);
            if (!_context.IsTemporaryAsset(state.motion))
            {
                // Protect the original animations from mutations by creating temporary clones; in the case of a proxy
                // animation, we'll restore the original in a later pass
                var placeholder = Object.Instantiate(state.motion);
                clipHolder.CurrentClip = placeholder;
                if (isProxyAnim)
                {
                    _clipCommitActions.Add(() => { Object.DestroyImmediate(placeholder); });
                }
                else
                {
                    RegisterMotionMapping(state.motion, placeholder);
                }
            }

            _clipCommitActions.Add(() => { state.motion = clipHolder.CurrentClip; });
        }

        private void RegisterMotionMapping(Motion original, Motion replacement)
        {
            ObjectRegistry.RegisterReplacedObject(original, replacement);

            if (original is BlendTree originalTree 
                && replacement is BlendTree replacementTree 
                && originalTree.children.Length == replacementTree.children.Length)
            {
                var originalChildren = originalTree.children;
                var replacementChildren = replacementTree.children;

                for (var i = 0; i < originalChildren.Length; i++)
                {
                    RegisterMotionMapping(originalChildren[i].motion, replacementChildren[i].motion);
                }
            }
        }

        internal void ForeachClip(Action<ClipHolder> processClip)
        {
            foreach (var clipHolder in _clips)
            {
                processClip(clipHolder);
            }
        }

        /// <summary>
        /// Returns a list of clips which touched the given _original_ path. This path is subject to basepath remapping,
        /// but not object movement remapping.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal ImmutableArray<ClipHolder> ClipsForPath(string path)
        {
            HydrateCaches();

            if (_pathToClip.TryGetValue(path, out var clips))
            {
                return clips.ToImmutableArray();
            }
            else
            {
                return ImmutableArray<ClipHolder>.Empty;
            }
        }

        private ClipHolder RegisterMotion(
            Motion motion,
            AnimatorState state,
            Action<ClipHolder> processClip,
            Dictionary<Motion, ClipHolder> originalToHolder
        )
        {
            if (motion == null)
            {
                return new ClipHolder(this, null);
            }

            if (originalToHolder.TryGetValue(motion, out var holder))
            {
                return holder;
            }

            InvalidateCaches();

            switch (motion)
            {
                case AnimationClip clip:
                {
                    holder = new ClipHolder(this, clip);
                    processClip(holder);
                    _clips.Add(holder);
                    break;
                }
                case BlendTree tree:
                {
                    holder = RegisterBlendtree(tree, state, processClip, originalToHolder);
                    break;
                }
            }

            originalToHolder[motion] = holder;
            return holder;
        }

        private void InvalidateCaches()
        {
            _pathToClip = null;
        }

        private void HydrateCaches()
        {
            if (_pathToClip == null)
            {
                _pathToClip = new Dictionary<string, HashSet<ClipHolder>>();
                foreach (var clip in _clips)
                {
                    recordPaths(clip);
                }
            }
        }

        private void recordPaths(ClipHolder holder)
        {
            var clip = holder.GetCurrentClipUnsafe() as AnimationClip;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var path = binding.path;
                AddPath(path);
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var path = binding.path;
                AddPath(path);
            }

            void AddPath(string p0)
            {
                if (!_pathToClip.TryGetValue(p0, out var clips))
                {
                    clips = new HashSet<ClipHolder>();
                    _pathToClip[p0] = clips;
                }

                clips.Add(holder);
            }
        }

        private ClipHolder RegisterBlendtree(
            BlendTree tree,
            AnimatorState state,
            Action<ClipHolder> processClip,
            Dictionary<Motion, ClipHolder> originalToHolder
        )
        {
            if (!_context.IsTemporaryAsset(tree))
            {
                throw new Exception("Blendtree must be a temporary asset");
            }

            var treeHolder = new ClipHolder(this, tree);

            var children = tree.children;
            var holders = new ClipHolder[children.Length];

            for (int i = 0; i < children.Length; i++)
            {
                holders[i] = RegisterMotion(children[i].motion, state, processClip, originalToHolder);
            }

            _clipCommitActions.Add(() =>
            {
                var dirty = false;
                for (int i = 0; i < children.Length; i++)
                {
                    var curClip = holders[i].CurrentClip;
                    if (children[i].motion != curClip)
                    {
                        children[i].motion = curClip;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    tree.children = children;
                    EditorUtility.SetDirty(tree);
                }
            });

            return treeHolder;
        }
    }
}