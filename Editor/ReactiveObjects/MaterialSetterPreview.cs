#if MA_VRCSDK3_AVATARS
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
            () => "Material Setter / Material Swap / Texture Swap",
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
        
        private PropCache<Renderer, ImmutableList<(int, object)>> _cache = new(
            "MaterialsForRenderer", MaterialsForRenderer, Enumerable.SequenceEqual
        );

        private static ImmutableList<(int, object)> MaterialsForRenderer(ComputeContext ctx, Renderer r)
        {
            if (r == null)
            {
                return ImmutableList<(int, object)>.Empty;
            }

            var avatar = ctx.GetAvatarRoot(r.gameObject);
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(ctx, avatar);

            var materials = ImmutableList<(int, object)>.Empty;
            
            foreach (var prop in analysis.Shapes.Values)
            {
                var target = prop.TargetProp;
                if (target.TargetObject != r) continue;
                if (!target.PropertyName.StartsWith(PREFIX)) continue;
                
                var index = int.Parse(target.PropertyName.Substring(PREFIX.Length, target.PropertyName.IndexOf(']') - PREFIX.Length));
                
                var activeRule = prop.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not Material and not MaterialOverride) continue;
                
                materials = materials.Add((index, activeRule.Value));
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
                
                var activeRule = prop.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not Material and not MaterialOverride) continue;

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
            var node = new Node(_cache, proxyPairs.First().Item1, context);

            return node.Refresh(null, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            private readonly Renderer _target;
            private readonly PropCache<Renderer, ImmutableList<(int, object)>> _cache;
            private ImmutableList<(int, object)> _materials = ImmutableList<(int, object)>.Empty;
            private ImmutableDictionary<MaterialOverride, Material> _generatedMaterials = ImmutableDictionary<MaterialOverride, Material>.Empty;
            
            public RenderAspects WhatChanged { get; private set; } = RenderAspects.Material;
            
            public Node(PropCache<Renderer, ImmutableList<(int, object)>> cache, Renderer renderer, ComputeContext context)
            {
                _cache = cache;
                _target = renderer;
                _generatedMaterials = _cache.Get(context, _target)
                    .Select(x => x.Item2)
                    .OfType<MaterialOverride>()
                    .Distinct()
                    .ToImmutableDictionary(x => x, x => x.ToMaterial());
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                if ((updatedAspects & (RenderAspects.Material | RenderAspects.Texture)) != 0 && _generatedMaterials.Count > 0)
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                var newMaterials = _cache.Get(context, _target);
                if (newMaterials.SequenceEqual(_materials))
                {
                    WhatChanged = 0;
                }
                else if (newMaterials
                    .Select(x => x.Item2)
                    .OfType<MaterialOverride>()
                    .All(_generatedMaterials.Keys.Contains))
                {
                    _materials = newMaterials;
                    WhatChanged = RenderAspects.Material;
                }
                else
                {
                    return Task.FromResult<IRenderFilterNode>(null);
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
                        switch (mat.Item2)
                        {
                            case Material material:
                                mats[mat.Item1] = material;
                                break;
                            case MaterialOverride materialOverride when _generatedMaterials.TryGetValue(materialOverride, out var material):
                                mats[mat.Item1] = material;
                                break;
                        }
                    }
                }

                proxy.sharedMaterials = mats;
            }

            public void Dispose()
            {
                foreach (var material in _generatedMaterials.Values)
                {
                    if (material != null)
                    {
                        Object.DestroyImmediate(material);
                    }
                }
                _generatedMaterials = ImmutableDictionary<MaterialOverride, Material>.Empty;
            }
        }
    }
}
#endif