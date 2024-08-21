#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor.ScaleAdjuster;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPreview : IRenderFilter
    {
        private static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Scale Adjuster",
            qualifiedName: "nadena.dev.modular-avatar/ScaleAdjusterPreview",
            true
        );
        
        [InitializeOnLoadMethod]
        private static void StaticInit()
        {
        }

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return EnableNode;
        }

        public bool IsEnabled(ComputeContext context)
        {
            return context.Observe(EnableNode.IsEnabled);
        }

        private static GameObject FindAvatarRootObserving(ComputeContext ctx, GameObject ptr)
        {
            while (ptr != null)
            {
                ctx.Observe(ptr);
                var xform = ptr.transform;
                if (RuntimeUtil.IsAvatarRoot(xform)) return ptr;

                ptr = xform.parent?.gameObject;
            }

            return null;
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext ctx)
        {
            var scaleAdjusters = ctx.GetComponentsByType<ModularAvatarScaleAdjuster>();

            var result = ImmutableList.CreateBuilder<RenderGroup>();

            foreach (var adjuster in scaleAdjusters)
            {
                if (adjuster == null) continue;

                // Find parent object
                // TODO: Reactive helper
                var root = FindAvatarRootObserving(ctx, adjuster.gameObject);
                if (root == null) continue;

                var renderers = ctx.GetComponentsInChildren<Renderer>(root, true);

                foreach (var renderer in renderers)
                    if (renderer is SkinnedMeshRenderer smr)
                        result.Add(RenderGroup.For(renderer));
            }

            return result.ToImmutable();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            return new ScaleAdjusterPreviewNode().Refresh(proxyPairs, context, 0);
        }
    }

    internal class ScaleAdjusterPreviewNode : IRenderFilterNode
    {
        private static ScaleAdjustedBones _bones = new ScaleAdjustedBones();

        public ScaleAdjusterPreviewNode()
        {
        }

        public RenderAspects Reads => 0;

        // We only change things in OnFrame, so downstream nodes will need to keep track of changes to these bones and
        // blendshapes themselves.
        public RenderAspects WhatChanged => 0;

        private readonly Dictionary<Transform, ModularAvatarScaleAdjuster> _boneOverrides
            = new(new ObjectIdentityComparer<Transform>());

        private Transform[] _boneArray, _newBoneArray;
        
        public Task<IRenderFilterNode> Refresh
        (
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            var pair = proxyPairs.First();
            Renderer original = pair.Item1;
            Renderer proxy = pair.Item2;

            if (original != null && proxy != null && original is SkinnedMeshRenderer smr)
            {
                _boneOverrides.Clear();

                foreach (var bone in smr.bones)
                {
                    var sa = bone?.GetComponent<ModularAvatarScaleAdjuster>();
                    if (sa != null) {
                        _boneOverrides.Add(bone, sa);
                    }
                }

                _boneArray = context.Observe(smr, s => s.bones, (b1, b2) =>
                {
                    // SequenceEqual is quite slow due to having to go through Unity native calls for each object, use
                    // reference equality instead
                    if (b1.Length != b2.Length) return false;

                    for (var i = 0; i < b1.Length; i++)
                        if (!ReferenceEquals(b1[i], b2[i]))
                            return false;

                    return true;
                });
                _newBoneArray = new Transform[_boneArray.Length];
            }
            
            return Task.FromResult((IRenderFilterNode)this);
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (_boneArray != null)
            {
                for (int i = 0; i < _boneArray.Length; i++)
                {
                    var b = _boneArray[i];
                    
                    ModularAvatarScaleAdjuster sa = null;
                    if (b != null) _boneOverrides.TryGetValue(b, out sa);
                    
                    _newBoneArray[i] = _bones.GetBone(sa, true)?.proxy ?? b;
                }
                
                ((SkinnedMeshRenderer)proxy).bones = _newBoneArray;
            }

            _bones.Update();
        }

        public void Dispose()
        {
            _bones.Clear();
        }
    }
}