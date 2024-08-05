using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ObjectSwitcherPreview : IRenderFilter
    {
        static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Object Switcher",
            qualifiedName: "nadena.dev.modular-avatar/ObjectSwitcherPreview",
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
            var allToggles = context.GetComponentsByType<ModularAvatarObjectToggle>();

            var objectGroups =
                new Dictionary<GameObject, ImmutableList<(ModularAvatarObjectToggle, int)>.Builder>(
                    new ObjectIdentityComparer<GameObject>());

            foreach (var toggle in allToggles)
            {
                if (!context.ActiveAndEnabled(toggle)) continue;

                var mami = context.GetComponent<ModularAvatarMenuItem>(toggle.gameObject);
                if (mami != null)
                    if (!context.Observe(mami, _ => mami.isDefault))
                        continue;

                context.Observe(toggle,
                    t => t.Objects.Select(o => o.Object.referencePath).ToList(),
                    (x, y) => x.SequenceEqual(y)
                );

                if (toggle.Objects == null) continue;

                var index = -1;
                foreach (var switched in toggle.Objects)
                {
                    index++;

                    if (switched.Object == null) continue;

                    var target = context.Observe(toggle, _ => switched.Object.Get(toggle));

                    if (target == null) continue;

                    if (!objectGroups.TryGetValue(target, out var group))
                    {
                        group = ImmutableList.CreateBuilder<(ModularAvatarObjectToggle, int)>();
                        objectGroups[target] = group;
                    }

                    group.Add((toggle, index));
                }
            }

            var affectedRenderers = objectGroups.Keys
                .SelectMany(go => context.GetComponentsInChildren<Renderer>(go, true))
                // If we have overlapping objects, we need to sort by child to parent, so parent configuration overrides
                // the child. We do this by simply looking at how many times we observe each renderer.
                .GroupBy(r => r)
                .Select(g => g.Key)
                .ToHashSet();

            var renderGroups = new List<RenderGroup>();
            
            foreach (var r in affectedRenderers)
            {
                var shouldEnable = true;
                
                var obj = r.gameObject;
                context.ActiveInHierarchy(obj); // observe path changes & object state changes
                
                while (obj != null)
                {
                    var enableAtNode = obj.activeSelf;
                    
                    var group = objectGroups.GetValueOrDefault(obj);
                    if (group == null && !obj.activeSelf)
                    {
                        // always inactive
                        shouldEnable = false;
                        break;
                    }

                    if (group != null)
                    {
                        var (toggle, index) = group[^1];
                        enableAtNode = context.Observe(toggle, t => t.Objects[index].Active);
                    }

                    if (!enableAtNode)
                    {
                        shouldEnable = false;
                        break;
                    }

                    obj = obj.transform.parent?.gameObject;
                }

                if (shouldEnable) renderGroups.Add(RenderGroup.For(r));
            }

            return renderGroups.ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            return Task.FromResult<IRenderFilterNode>(new Node());
        }

        private class Node : IRenderFilterNode
        {
            public RenderAspects WhatChanged => 0;

      
            public void OnFrame(Renderer original, Renderer proxy)
            {
                proxy.gameObject.SetActive(true);
            }
        }
    }
}