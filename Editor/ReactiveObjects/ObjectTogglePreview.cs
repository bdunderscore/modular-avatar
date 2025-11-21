using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ObjectSwitcherPreview : IRenderFilter
    {
        public bool CanEnableRenderers => true;

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

        private IEnumerable<RenderGroup> RootsForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            if (!context.ActiveInHierarchy(avatarRoot))
            {
                yield break;
            }

            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);
            var initialStates = analysis.InitialStates;
            
            var renderers = context.GetComponentsInChildren<Renderer>(avatarRoot, true);

            foreach (var renderer in renderers)
            {
                // For now, the preview system only supports MeshRenderer and SkinnedMeshRenderer
                if (renderer is not MeshRenderer and not SkinnedMeshRenderer) continue;

                bool currentlyEnabled = context.ActiveInHierarchy(renderer.gameObject);

                bool overrideEnabled = true;
                Transform cursor = renderer.transform;
                while (cursor != null && !RuntimeUtil.IsAvatarRoot(cursor))
                {
                    if (initialStates.TryGetValue(TargetProp.ForObjectActive(cursor.gameObject), out var initialState) && initialState is float f)
                    {
                        if (f < 0.5f)
                        {
                            overrideEnabled = false;
                            break;
                        }
                    }
                    else if (!cursor.gameObject.activeSelf)
                    {
                        overrideEnabled = false;
                        break;
                    }
                    
                    cursor = cursor.parent;
                }

                if (overrideEnabled != currentlyEnabled)
                {
                    yield return RenderGroup.For(renderer).WithData(overrideEnabled);
                }
            }
        }
        
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var roots = context.GetAvatarRoots();
            
            return roots.SelectMany(av => RootsForAvatar(context, av)).ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            return Task.FromResult<IRenderFilterNode>(new Node(group.GetData<bool>()));
        }

        private class Node : IRenderFilterNode
        {
            public RenderAspects WhatChanged => 0;
            private readonly bool _shouldEnable;

            public Node(bool shouldEnable)
            {
                _shouldEnable = shouldEnable;
            }
      
            public void OnFrame(Renderer original, Renderer proxy)
            {
                proxy.enabled = _shouldEnable;
            }
        }
    }
}