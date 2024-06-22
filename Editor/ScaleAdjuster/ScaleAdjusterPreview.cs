#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor.ScaleAdjuster;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.rq;
using nadena.dev.ndmf.rq.unity.editor;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPreview : IRenderFilter
    {
        
        [InitializeOnLoadMethod]
        private static void StaticInit()
        {
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

        public ReactiveValue<ImmutableList<RenderGroup>> TargetGroups { get; } =
            ReactiveValue<ImmutableList<RenderGroup>>.Create(
            "Scale Adjuster: Find targets",
            async ctx =>
            {
                var scaleAdjusters = await ctx.Observe(CommonQueries.GetComponentsByType<ModularAvatarScaleAdjuster>());

                ImmutableList<RenderGroup>.Builder result = ImmutableList.CreateBuilder<RenderGroup>();
                
                foreach (var adjuster in scaleAdjusters)
                {
                    // Find parent object
                    // TODO: Reactive helper
                    var root = FindAvatarRootObserving(ctx, adjuster.gameObject);
                    if (root == null) continue;

                    var renderers = ctx.GetComponentsInChildren<Renderer>(root, true);

                    foreach (var renderer in renderers)
                    {
                        if (renderer is SkinnedMeshRenderer smr)
                        {
                            result.Add(RenderGroup.For(renderer));
                        }
                    }
                }

                return result.ToImmutable();
            });

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            return Task.FromResult((IRenderFilterNode)new ScaleAdjusterPreviewNode());
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

        public Task<IRenderFilterNode> Refresh
        (
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            return Task.FromResult((IRenderFilterNode)this);
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (proxy is SkinnedMeshRenderer p_smr && original is SkinnedMeshRenderer o_smr)
            {
                p_smr.rootBone = _bones.GetBone(o_smr.rootBone)?.proxy ?? o_smr.rootBone;
                p_smr.bones = o_smr.bones.Select(b =>
                {
                    var sa = (Component)b?.GetComponent<ModularAvatarScaleAdjuster>();
                    return _bones.GetBone(sa ?? b, true)?.proxy ?? b;
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