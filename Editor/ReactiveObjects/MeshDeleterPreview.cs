#if MA_VRCSDK3_AVATARS
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public class MeshDeleterPreview : IRenderFilter
    {
        private static TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Mesh Deleter",
            qualifiedName: "nadena.dev.modular-avatar/MeshDeleterPreview",
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

        private readonly PropCache<SkinnedMeshRenderer, ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)>> _cache = new(
            "GetDeletionsForRenderer", GetDeletionsForRenderer, Enumerable.SequenceEqual);

        private static ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)> GetDeletionsForRenderer(ComputeContext context, Renderer renderer)
        {
            if (renderer == null)
            {
                return ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)>.Empty;
            }

            var avatar = context.GetAvatarRoot(renderer.gameObject);
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatar);
            var deletions = ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)>.Empty;

            foreach (var property in analysis.Shapes.Values)
            {
                var prop = property.TargetProp;
                if (prop.TargetObject != renderer) continue;
                if (!prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedMeshPrefix)) continue;

                var split = prop.PropertyName[ReactiveObjectAnalyzer.DeletedMeshPrefix.Length..].Split('.');
                var materialIndex = int.Parse(split[0]);
                var maskTexture = EditorUtility.InstanceIDToObject(int.Parse(split[1])) as Texture2D;

                var activeRule = property.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not MeshDeleteMode deleteMode) continue;

                deletions = deletions.Add((materialIndex, maskTexture, maskTexture.imageContentsHash, deleteMode));
            }

            return deletions;
        }

        private IEnumerable<RenderGroup> GroupsForAvatar(ComputeContext context, GameObject avatarRoot)
        {
            var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, avatarRoot);
            var renderers = new HashSet<Renderer>();

            foreach (var property in analysis.Shapes.Values)
            {
                var prop = property.TargetProp;
                if (prop.TargetObject is not SkinnedMeshRenderer renderer) continue;
                if (!prop.PropertyName.StartsWith(ReactiveObjectAnalyzer.DeletedMeshPrefix)) continue;

                var activeRule = property.actionGroups.LastOrDefault(r => r.InitiallyActive);
                if (activeRule == null || activeRule.Value is not MeshDeleteMode) continue;

                renderers.Add(renderer);
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
            var (original, proxy) = proxyPairs.First();
            var node = new Node(_cache, original as SkinnedMeshRenderer, proxy as SkinnedMeshRenderer, context);
            return node.Refresh(null, context, 0);
        }

        private class Node : IRenderFilterNode
        {
            private readonly PropCache<SkinnedMeshRenderer, ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)>> _cache;
            private readonly SkinnedMeshRenderer _original;
            private readonly ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)> _deletions;
            private Mesh _generatedMesh;

            public RenderAspects WhatChanged => RenderAspects.Mesh;

            internal Node(PropCache<SkinnedMeshRenderer, ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)>> cache,
                SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, ComputeContext context)
            {
                _cache = cache;
                _original = original;
                _deletions = _cache.Get(context, _original);
                _generatedMesh = GenerateMesh(proxy.sharedMesh, _deletions);
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
            {
                if ((updatedAspects & RenderAspects.Mesh) != 0)
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                var deletions = _cache.Get(context, _original);
                if (!deletions.SequenceEqual(_deletions))
                {
                    return Task.FromResult<IRenderFilterNode>(null);
                }

                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (original == null || proxy == null) return;
                if (original is not SkinnedMeshRenderer || proxy is not SkinnedMeshRenderer renderer) return;

                if (_generatedMesh != null)
                {
                    renderer.sharedMesh = _generatedMesh;
                }
            }

            private Mesh GenerateMesh(Mesh mesh, ImmutableList<(int, Texture2D, Hash128, MeshDeleteMode)> deletions)
            {
                if (deletions.All(x => x.Item4 == MeshDeleteMode.DontDelete))
                {
                    return null;
                }

                mesh = Object.Instantiate(mesh);

                var vertexFilter = new VertexFilterByMask(mesh);
                var vertexMask = new bool[mesh.vertexCount];
                foreach (var deletion in deletions)
                {
                    vertexFilter.Filter((deletion.Item1, deletion.Item2, deletion.Item4), vertexMask);
                }

                var tris = new List<int>();
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    tris.Clear();

                    var baseVertex = (int)mesh.GetBaseVertex(subMesh);
                    mesh.GetTriangles(tris, subMesh, false);

                    for (var i = 0; i < tris.Count; i += 3)
                    {
                        if (vertexMask[tris[i + 0] + baseVertex] ||
                            vertexMask[tris[i + 1] + baseVertex] ||
                            vertexMask[tris[i + 2] + baseVertex])
                        {
                            tris.RemoveRange(i, 3);
                            i -= 3;
                        }
                    }

                    mesh.SetTriangles(tris, subMesh, false, baseVertex: baseVertex);
                }

                return mesh;
            }

            public void Dispose()
            {
                if (_generatedMesh != null)
                {
                    Object.DestroyImmediate(_generatedMesh);
                    _generatedMesh = null;
                }
            }
        }
    }
}
#endif
