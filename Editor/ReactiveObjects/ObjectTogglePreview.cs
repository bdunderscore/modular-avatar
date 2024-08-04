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
                .ToList();

            var renderGroups = new List<RenderGroup>();

            foreach (var r in affectedRenderers)
            {
                var switchers = new List<(ModularAvatarObjectToggle, int)>();

                var obj = r.gameObject;
                while (obj != null)
                {
                    var group = objectGroups.GetValueOrDefault(obj);
                    if (group != null) switchers.AddRange(group);

                    obj = obj.transform.parent?.gameObject;
                }

                renderGroups.Add(RenderGroup.For(r).WithData(switchers.ToImmutableList()));
            }

            return renderGroups.ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            var data = group.GetData<ImmutableList<(ModularAvatarObjectToggle, int)>>();
            return new Node(data).Refresh(proxyPairs, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            public RenderAspects WhatChanged => 0;

            private readonly ImmutableList<(ModularAvatarObjectToggle, int)> _controllers;

            public Node(ImmutableList<(ModularAvatarObjectToggle, int)> controllers)
            {
                _controllers = controllers;
            }
            
            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context,
                RenderAspects updatedAspects)
            {
                foreach (var controller in _controllers)
                {
                    // Ensure we get awoken whenever there's a change in a controlling component, or its enabled state.
                    context.Observe(controller.Item1);
                    context.ActiveAndEnabled(controller.Item1);
                }

                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                var shouldEnable = true;
                foreach (var (controller, index) in _controllers)
                {
                    if (controller == null) continue;
                    if (!controller.gameObject.activeInHierarchy) continue;
                    if (controller.Objects == null || index >= controller.Objects.Count) continue;

                    var obj = controller.Objects[index];
                    shouldEnable = obj.Active;
                }

                proxy.gameObject.SetActive(shouldEnable);
            }
        }
    }
}