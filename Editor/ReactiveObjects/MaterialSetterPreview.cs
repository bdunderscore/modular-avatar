using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public class MaterialSetterPreview : IRenderFilter
    {
        static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Material Setter",
            qualifiedName: "nadena.dev.modular-avatar/MaterialSetterPreview",
            true
        );
        
        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return EnableNode;
        }

        public bool IsEnabled(ComputeContext context)
        {
            return context.Observe(EnableNode.IsEnabled);
        }

        private const string PREFIX = "m_Materials.Array.data[";
        
        private PropCache<Renderer, ImmutableList<(int, Material)>> _cache = new(
            "GetMaterialOverridesForRenderer", GetMaterialOverridesForRenderer, Enumerable.SequenceEqual
        );

        private static ImmutableList<(int, Material)> GetMaterialOverridesForRenderer(ComputeContext ctx, Renderer r)
        {
            if (r == null)
            {
                return ImmutableList<(int, Material)>.Empty;
            }

            var avatar = ctx.GetAvatarRoot(r.gameObject);
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(ctx, avatar);

            var materials = ImmutableList<(int, Material)>.Empty;
            
            foreach (var prop in analysis.Shapes.Values)
            {
                var target = prop.TargetProp;
                if (target.TargetObject != r) continue;
                if (!target.PropertyName.StartsWith(PREFIX)) continue;
                
                var index = int.Parse(target.PropertyName.Substring(PREFIX.Length, target.PropertyName.IndexOf(']') - PREFIX.Length));
                
                var activeRule = prop.actionGroups.FirstOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not Material mat) continue;
                
                materials = materials.Add((index, mat));
            }

            return materials.OrderBy(kv => kv.Item1).ToImmutableList();
        }

        private IEnumerable<RenderGroup> GroupsForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);

            HashSet<Renderer> renderers = new();
            
            foreach (var prop in analysis.Shapes.Values)
            {
                var target = prop.TargetProp;
                if (target.TargetObject is not Renderer r || r == null) continue;
                if (target.TargetObject is not MeshRenderer and not SkinnedMeshRenderer) continue;
                if (!target.PropertyName.StartsWith(PREFIX)) continue;
                
                var index = int.Parse(target.PropertyName.Substring(PREFIX.Length, target.PropertyName.IndexOf(']') - PREFIX.Length));
                
                var activeRule = prop.actionGroups.FirstOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not Material mat) continue;

                renderers.Add(r);
            }
            
            return renderers.Select(RenderGroup.For);
        }
        
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var avatarRoots = context.GetAvatarRoots();
            return avatarRoots.SelectMany(r => GroupsForAvatar(context, r)).ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var node = new Node(_cache, proxyPairs.First().Item1);

            return node.Refresh(null, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            private readonly Renderer _target;
            private readonly PropCache<Renderer, ImmutableList<(int, Material)>> _cache;
            private ImmutableList<(int, Material)> _materials = ImmutableList<(int, Material)>.Empty;
            
            public RenderAspects WhatChanged { get; private set; } = RenderAspects.Material;
            
            public Node(PropCache<Renderer, ImmutableList<(int, Material)>> cache, Renderer renderer)
            {
                _cache = cache;
                _target = renderer;
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                var newMaterials = _cache.Get(context, _target);

                if (newMaterials.SequenceEqual(_materials))
                {
                    WhatChanged = 0;
                } else {
                    _materials = newMaterials;
                    WhatChanged = RenderAspects.Material;
                }
                
                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (original == null || proxy == null) return;

                var mats = proxy.sharedMaterials;

                foreach (var mat in _materials)
                {
                    if (mat.Item1 < mats.Length)
                    {
                        mats[mat.Item1] = mat.Item2;
                    }
                }

                proxy.sharedMaterials = mats;
            }
        }
    }
}