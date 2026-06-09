#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByBoneComponent))]
    internal sealed class VertexFilterByBone : IMeshSelector
    {
        private readonly Transform? _bone;
        private readonly float _threshold;
        private readonly VertexSelectionMode _selectionMode;

        public VertexFilterByBone(
            Transform bone,
            float threshold,
            VertexSelectionMode selectionMode = VertexSelectionMode.AnyVertex
        )
        {
            _bone = bone;
            _threshold = threshold;
            _selectionMode = NormalizeSelectionMode(selectionMode);
        }

        public VertexFilterByBone(VertexFilterByBoneComponent component, ComputeContext context)
        {
            (_bone, _threshold, _selectionMode) = context.Observe(
                component,
                c => (c.Bone.Get(c)?.transform, c.Threshold, NormalizeSelectionMode(c.SelectionMode))
            );
        }

        private static VertexSelectionMode NormalizeSelectionMode(VertexSelectionMode selectionMode)
        {
            return selectionMode == VertexSelectionMode.Centroid ? VertexSelectionMode.AnyVertex : selectionMode;
        }

        public bool Equals(IMeshSelector other)
        {
            return other is VertexFilterByBone filter
                   && filter._bone == _bone
                   && Mathf.Approximately(filter._threshold, _threshold)
                   && filter._selectionMode == _selectionMode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(typeof(VertexFilterByBone), _bone, _threshold, _selectionMode);
        }

        public override string ToString()
        {
            return $"VertexFilterByBone: {(_bone != null ? _bone.name : "null")} @ {_threshold}";
        }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            return job.MarkPrimitivesFromVertexIndices(
                this,
                () => BuildVertexMask(job),
                _selectionMode,
                submesh,
                selectedPrimitives
            );
        }

        private (NativeArray<bool>, JobHandle) BuildVertexMask(MeshSelectorJob job)
        {
            var mesh = job.OriginalMesh;
            var vertexMask = new NativeArray<bool>(mesh.vertexCount, Allocator.TempJob);

            if (_bone == null || !(job.ReferenceRenderer is SkinnedMeshRenderer smr))
                return (vertexMask, default);

            var targetBoneRef = ObjectRegistry.GetReference(_bone);
            if (targetBoneRef == null) return (vertexMask, default);

            var boneIndices = new List<int>();
            for (int i = 0; i < smr.bones.Length; i++)
            {
                if (smr.bones[i] != null &&
                    (ObjectRegistry.GetReference(smr.bones[i]) == targetBoneRef || smr.bones[i] == _bone))
                {
                    boneIndices.Add(i);
                }
            }

            if (boneIndices.Count == 0) return (vertexMask, default);

            var boneWeights = mesh.GetAllBoneWeights();
            var bonesPerVertex = mesh.GetBonesPerVertex();

            if (bonesPerVertex.Length == 0 || boneWeights.Length == 0)
                return (vertexMask, default);

            int weightIndex = 0;
            for (int v = 0; v < bonesPerVertex.Length; v++)
            {
                int numWeights = bonesPerVertex[v];
                if (numWeights > 0)
                {
                    float totalWeight = 0f, targetWeight = 0f;
                    for (int w = 0; w < numWeights; w++)
                    {
                        var bw = boneWeights[weightIndex + w];
                        if (boneIndices.Contains(bw.boneIndex)) targetWeight += bw.weight;
                        totalWeight += bw.weight;
                    }

                    if (targetWeight > 0 && targetWeight / totalWeight >= _threshold)
                        vertexMask[v] = true;

                    weightIndex += numWeights;
                }
            }

            return (vertexMask, default);
        }
    }
}
