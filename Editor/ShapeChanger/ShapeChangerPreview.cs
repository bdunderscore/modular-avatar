#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.rq;
using nadena.dev.ndmf.rq.unity.editor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    public class ShapeChangerPreview : IRenderFilter
    {
        private static ReactiveValue<ImmutableDictionary<Renderer, ImmutableList<ModularAvatarShapeChanger>>>
            InternalTargetGroups
                = ReactiveValue<ImmutableDictionary<Renderer, ImmutableList<ModularAvatarShapeChanger>>>.Create(
                    "ShapeChangerPreview.TargetGroups", async ctx =>
                    {
                        var allChangers =
                            await ctx.Observe(CommonQueries.GetComponentsByType<ModularAvatarShapeChanger>());

                        Dictionary<Renderer, ImmutableList<ModularAvatarShapeChanger>.Builder> groups =
                            new Dictionary<Renderer, ImmutableList<ModularAvatarShapeChanger>.Builder>(
                                new ObjectIdentityComparer<Renderer>());

                        foreach (var changer in allChangers)
                        {
                            // TODO: observe avatar root
                            ctx.Observe(changer);
                            var target = ctx.Observe(changer.targetRenderer.Get(changer));
                            var renderer = ctx.GetComponent<SkinnedMeshRenderer>(target);

                            if (renderer == null) continue;

                            if (!groups.TryGetValue(renderer, out var group))
                            {
                                group = ImmutableList.CreateBuilder<ModularAvatarShapeChanger>();
                                groups[renderer] = group;
                            }

                            group.Add(changer);
                        }

                        return groups.ToImmutableDictionary(p => p.Key, p => p.Value.ToImmutable());
                    });

        public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups { get; } =
            ReactiveValue<IImmutableList<IImmutableList<Renderer>>>.Create(
                "ShapeChangerPreview.TargetGroups", async ctx =>
                {
                    var targetGroups = await ctx.Observe(InternalTargetGroups);

                    return targetGroups.Keys
                        .Select(v => (IImmutableList<Renderer>)ImmutableList.Create(v))
                        .ToImmutableList();
                });

        private bool IsChangerActive(ModularAvatarShapeChanger changer, ComputeContext context)
        {
            if (context != null)
            {
                if (!context.ActiveAndEnabled(changer)) return false;
            }
            else
            {
                if (!changer.isActiveAndEnabled) return false;
            }
            
            var changerRenderer = context != null
                ? context.GetComponent<Renderer>(changer.gameObject)
                : changer.GetComponent<Renderer>();
            context?.Observe(changerRenderer);

            if (changerRenderer == null) return false;
            return changerRenderer.enabled && (context?.ActiveAndEnabled(changer) ?? changer.isActiveAndEnabled);
        }


        public async Task MutateMeshData(IList<MeshState> states, ComputeContext context)
        {
            var targetGroups = await context.Observe(InternalTargetGroups);

            var state = states[0];
            var renderer = state.Original;
            if (renderer == null) return;
            if (!targetGroups.TryGetValue(renderer, out var changers)) return;
            if (!(renderer is SkinnedMeshRenderer smr)) return;

            HashSet<int> toDelete = new HashSet<int>();

            foreach (var changer in changers)
            {
                if (!IsChangerActive(changer, context)) continue;

                foreach (var shape in changer.Shapes)
                {
                    if (shape.ChangeType == ShapeChangeType.Delete)
                    {
                        var index = state.Mesh.GetBlendShapeIndex(shape.ShapeName);
                        if (index < 0) continue;
                        toDelete.Add(index);
                    }
                }
            }

            if (toDelete.Count > 0)
            {
                var mesh = Object.Instantiate(state.Mesh);

                var bsPos = new Vector3[mesh.vertexCount];
                bool[] targetVertex = new bool[mesh.vertexCount];
                foreach (var bs in toDelete)
                {
                    int frames = mesh.GetBlendShapeFrameCount(bs);
                    for (int f = 0; f < frames; f++)
                    {
                        mesh.GetBlendShapeFrameVertices(bs, f, bsPos, null, null);

                        for (int i = 0; i < bsPos.Length; i++)
                        {
                            if (bsPos[i].sqrMagnitude > 0.0001f)
                            {
                                targetVertex[i] = true;
                            }
                        }
                    }
                }

                List<int> tris = new List<int>();
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    tris.Clear();

                    var baseVertex = (int)mesh.GetBaseVertex(subMesh);
                    mesh.GetTriangles(tris, subMesh, false);

                    for (int i = 0; i < tris.Count; i += 3)
                    {
                        if (targetVertex[tris[i] + baseVertex] || targetVertex[tris[i + 1] + baseVertex] ||
                            targetVertex[tris[i + 2] + baseVertex])
                        {
                            tris.RemoveRange(i, 3);
                            i -= 3;
                        }
                    }

                    mesh.SetTriangles(tris, subMesh, false, baseVertex: baseVertex);
                }

                state.Mesh = mesh;
                state.OnDispose += () =>
                {
                    if (mesh != null) Object.Destroy(mesh);
                };
            }
        }


        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (!InternalTargetGroups.TryGetValue(out var targetGroups)) return;
            if (!targetGroups.TryGetValue(original, out var changers)) return;
            if (!(proxy is SkinnedMeshRenderer smr)) return;

            var mesh = smr.sharedMesh;
            if (mesh == null) return;

            foreach (var changer in changers)
            {
                if (!IsChangerActive(changer, null)) continue;

                foreach (var shape in changer.Shapes)
                {
                    var index = mesh.GetBlendShapeIndex(shape.ShapeName);
                    if (index < 0) continue;

                    float setToValue = -1;

                    switch (shape.ChangeType)
                    {
                        case ShapeChangeType.Delete:
                            setToValue = 100;
                            break;
                        case ShapeChangeType.Set:
                            setToValue = shape.Value;
                            break;
                    }

                    smr.SetBlendShapeWeight(index, setToValue);
                }
            }
        }
    }
}