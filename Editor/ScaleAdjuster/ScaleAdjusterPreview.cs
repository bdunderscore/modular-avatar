#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor.plugin;
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
            }
            
            return Task.FromResult((IRenderFilterNode)this);
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (proxy is SkinnedMeshRenderer p_smr && original is SkinnedMeshRenderer o_smr)
            {
                p_smr.rootBone = _bones.GetBone(o_smr.rootBone)?.proxy ?? o_smr.rootBone;
                p_smr.bones = o_smr.bones.Select(b =>
                {
                    ModularAvatarScaleAdjuster sa = null;
                    if (b != null) _boneOverrides.TryGetValue(b, out sa);
                    
                    return _bones.GetBone(sa, true)?.proxy ?? b;
                }).ToArray();
            }

            _bones.Update();
        }

        public void Dispose()
        {
            _bones.Clear();
        }
    }
}