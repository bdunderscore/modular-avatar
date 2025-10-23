#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal class HideOtherAvatarsFilter : IRenderFilter
    {
        public readonly PublishedValue<Object> targetAvatar = new(null);

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var targetAvatar = context.Observe(this.targetAvatar,
                o => o as GameObject,
                (a, b) => a == b
            );

            return context.GetAvatarRoots()
                .Where(root => root != targetAvatar)
                .SelectMany(root => context.GetComponentsInChildren<Renderer>(root, true))
                .Where(r => r is MeshRenderer or SkinnedMeshRenderer)
                .Select(RenderGroup.For)
                .ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            return Task.FromResult(HideAllNode.Instance as IRenderFilterNode);
        }

        private class HideAllNode : IRenderFilterNode
        {
            internal static readonly HideAllNode Instance = new();

            public RenderAspects WhatChanged => 0;

            public void OnFrame(Renderer original, Renderer proxy)
            {
                proxy.enabled = false;
            }
        }
    }
}