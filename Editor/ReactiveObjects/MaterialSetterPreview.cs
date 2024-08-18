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

        
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var menuItemPreview = new MenuItemPreviewCondition(context);
            var setters = context.GetComponentsByType<ModularAvatarMaterialSetter>();

            var groups = new Dictionary<Renderer, ImmutableList<ModularAvatarMaterialSetter>>();
            
            foreach (var setter in setters)
            {
                var mami = context.GetComponent<ModularAvatarMenuItem>(setter.gameObject);
                bool active = context.ActiveAndEnabled(setter) && (mami == null || menuItemPreview.IsEnabledForPreview(mami));
                if (active == context.Observe(setter, t => t.Inverted)) continue;
                
                var overrideTarget = context.Observe(setter, c => c.targetRenderer.Get(setter));
                var overrideRenderer = context.GetComponent<Renderer>(overrideTarget);

                var objs = context.Observe(setter, s => s.Objects.Select(o => (o.Object.Get(s), o.Material, o.MaterialIndex)).ToList(), (x, y) => x.SequenceEqual(y));
                
                foreach (var (obj, mat, index) in objs)
                {
                    var renderer = overrideRenderer ?? context.GetComponent<Renderer>(obj);
                    if (renderer == null) continue;
                    
                    var matCount = context.Observe(renderer, r => r.sharedMaterials.Length);
                    
                    if (matCount <= index) continue;
                    
                    if (!groups.TryGetValue(renderer, out var list))
                    {
                        list = ImmutableList.Create<ModularAvatarMaterialSetter>();
                        groups.Add(renderer, list);
                    }
                    
                    groups[renderer] = list.Add(setter);
                }
            }

            var finalGroups = groups.Select(g => RenderGroup.For(g.Key).WithData(g.Value)).ToImmutableList();
            return finalGroups;
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var setters = group.GetData<ImmutableList<ModularAvatarMaterialSetter>>();
            var node = new Node(setters);

            return node.Refresh(proxyPairs, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            private readonly ImmutableList<ModularAvatarMaterialSetter> _setters;
            private ImmutableList<(int, Material)> _materials;
            
            public RenderAspects WhatChanged {get; private set; }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                var mats = proxy.sharedMaterials;

                foreach (var mat in _materials)
                {
                    if (mat.Item1 <= mats.Length)
                    {
                        mats[mat.Item1] = mat.Item2;
                    }
                }
                
                proxy.sharedMaterials = mats;
            }
            
            public Node(ImmutableList<ModularAvatarMaterialSetter> setters)
            {
                _setters = setters;
                _materials = ImmutableList<(int, Material)>.Empty;
                WhatChanged = RenderAspects.Material;
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                var proxyPair = proxyPairs.First();
                var original = proxyPair.Item1;
                var proxy = proxyPair.Item2;

                var mats = new Material[proxy.sharedMaterials.Length];
                
                foreach (var setter in _setters)
                {
                    var overrideTarget = context.Observe(setter, c => c.targetRenderer.Get(setter));
                    var overrideRenderer = context.GetComponent<Renderer>(overrideTarget);

                    var objs = context.Observe(setter, s => s.Objects.Select(o => (o.Object.Get(s), o.Material, o.MaterialIndex)).ToList(), (x, y) => x.SequenceEqual(y));

                    foreach (var (obj, mat, index) in objs)
                    {
                        var renderer = overrideRenderer ?? context.GetComponent<Renderer>(obj);
                        if (renderer != original) continue;

                        if (index <= mats.Length)
                        {
                            mats[index] = mat;
                        }
                    }
                }
                
                var materials = mats.Select((m, i) => (i, m)).Where(kvp => kvp.m != null).ToImmutableList();

                if (materials.SequenceEqual(_materials))
                {
                    return Task.FromResult<IRenderFilterNode>(this);
                }
                else
                {
                    return Task.FromResult<IRenderFilterNode>(new Node(_setters) { _materials = materials });
                }
            }
        }
    }
}