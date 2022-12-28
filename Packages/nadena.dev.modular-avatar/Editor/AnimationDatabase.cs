using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class AnimationDatabase
    {
        internal class ClipHolder
        {
            internal Motion CurrentClip;
            internal Motion OriginalClip { get; }

            internal ClipHolder(Motion clip)
            {
                CurrentClip = OriginalClip = clip;
            }
        }

        private List<Action> _clipCommitActions = new List<Action>();
        private List<ClipHolder> _clips = new List<ClipHolder>();

        private Dictionary<string, HashSet<ClipHolder>> _pathToClip =
            new Dictionary<string, HashSet<ClipHolder>>();

        private HashSet<BlendTree> _processedBlendTrees = new HashSet<BlendTree>();

        internal void Commit()
        {
            foreach (var action in _clipCommitActions)
            {
                action();
            }
        }

        internal void Bootstrap(VRCAvatarDescriptor avatarDescriptor)
        {
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
                if (!layer.isDefault && layer.animatorController is AnimatorController ac && Util.IsTemporaryAsset(ac))
                {
                    foreach (var state in Util.States(ac))
                    {
                        RegisterState(state);
                    }
                }
            }
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
            if (!Util.IsTemporaryAsset(state))
            {
                throw new Exception("State must be a temporary asset");
            }

            if (state.motion == null) return;

            RegisterMotion(state.motion, state, processClip, _originalToHolder);
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
            if (originalToHolder.TryGetValue(motion, out var holder))
            {
                return holder;
            }

            switch (motion)
            {
                case AnimationClip clip:
                {
                    holder = new ClipHolder(clip);
                    processClip(holder);
                    recordPaths(holder);
                    _clips.Add(holder);
                    _clipCommitActions.Add(() =>
                    {
                        if (holder.CurrentClip != holder.OriginalClip)
                        {
                            state.motion = holder.CurrentClip;
                            if (!AssetDatabase.IsSubAsset(holder.CurrentClip))
                            {
                                AssetDatabase.AddObjectToAsset(holder.CurrentClip, AssetDatabase.GetAssetPath(state));
                            }

                            EditorUtility.SetDirty(state);
                        }
                    });
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

        private void recordPaths(ClipHolder holder)
        {
            var clip = holder.CurrentClip as AnimationClip;

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
            if (!Util.IsTemporaryAsset(tree))
            {
                throw new Exception("Blendtree must be a temporary asset");
            }

            var treeHolder = new ClipHolder(tree);

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
                    if (children[i].motion != holders[i].CurrentClip)
                    {
                        children[i].motion = holders[i].CurrentClip;
                        dirty = true;
                        if (!AssetDatabase.IsSubAsset(holders[i].CurrentClip))
                        {
                            AssetDatabase.AddObjectToAsset(holders[i].CurrentClip, AssetDatabase.GetAssetPath(state));
                        }
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