#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class RemoveVertexColorPreview : IRenderFilter
    {
        private static string ToPathString(ComputeContext ctx, Transform t)
        {
            return string.Join("/", ctx.ObservePath(t).Select(t2 => t2.gameObject.name).Reverse());
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var roots = context.GetAvatarRoots()
                .Where(r => context.ActiveInHierarchy(r) is true);
            var removers = roots
                .SelectMany(r => context.GetComponentsInChildren<ModularAvatarRemoveVertexColor>(r, true))
                .Select(rvc => (ToPathString(context, rvc.transform),
                    context.Observe(rvc, r => r.Mode) == ModularAvatarRemoveVertexColor.RemoveMode.Remove))
                .OrderBy(pair => pair.Item1)
                .ToList();
            var targets = roots.SelectMany(
                r => context.GetComponentsInChildren<SkinnedMeshRenderer>(r, true)
                    .Concat(
                        context.GetComponentsInChildren<MeshFilter>(r, true)
                            .SelectMany(mf => context.GetComponents<Renderer>(mf.gameObject))
                    )
            );

            targets = targets.Where(target =>
            {
                var stringPath = ToPathString(context, target.transform);
                var index = removers.BinarySearch((stringPath, true));

                if (index >= 0)
                {
                    // There is a component on this mesh
                    return true;
                }

                var priorIndex = ~index - 1;
                if (priorIndex < 0) return false; // no match

                var (maybeParent, mode) = removers[priorIndex];
                if (!stringPath.StartsWith(maybeParent)) return false; // no parent matched
                return mode;
            });

            return targets.Select(RenderGroup.For).ToImmutableList();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context)
        {
            Dictionary<Mesh, Mesh> conversionMap = new();

            foreach (var (_, proxy) in proxyPairs)
            {
                Component c = proxy;
                if (!(c is SkinnedMeshRenderer))
                {
                    c = context.GetComponent<MeshFilter>(proxy.gameObject);
                }

                if (c == null) continue;

                RemoveVertexColorPass.ForceRemove(_ => false, c, conversionMap);
            }

            return Task.FromResult<IRenderFilterNode>(new Node(conversionMap.Values.FirstOrDefault()));
        }

        private class Node : IRenderFilterNode
        {
            private readonly Mesh? _theMesh;

            public Node(Mesh? theMesh)
            {
                _theMesh = theMesh;
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context,
                RenderAspects updatedAspects)
            {
                if (updatedAspects.HasFlag(RenderAspects.Mesh)) return Task.FromResult<IRenderFilterNode>(null!);
                if (_theMesh == null) return Task.FromResult<IRenderFilterNode>(null!);

                return Task.FromResult<IRenderFilterNode>(this);
            }

            public RenderAspects WhatChanged => RenderAspects.Mesh;

            public void Dispose()
            {
                if (_theMesh != null) Object.DestroyImmediate(_theMesh);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (_theMesh == null) return;

                switch (proxy)
                {
                    case SkinnedMeshRenderer smr: smr.sharedMesh = _theMesh; break;
                    default:
                    {
                        var mf = proxy.GetComponent<MeshFilter>();
                        if (mf != null) mf.sharedMesh = _theMesh;
                        break;
                    }
                }
            }
        }
    }
}