#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByBoneComponent))]
    internal sealed class VertexFilterByBone : IVertexFilter
    {
        private readonly Transform _bone;
        private readonly float _threshold;
        private readonly Transform _avatarRoot;

        public VertexFilterByBone(Transform bone, float threshold)
        {
            _bone = bone;
            _threshold = threshold;
            _avatarRoot = null;
        }
        
        public VertexFilterByBone(VertexFilterByBoneComponent component, ComputeContext context)
        {
            (_bone, _threshold) = context.Observe(component, c => (c.Bone.Get(c)?.transform, c.Threshold));
            _avatarRoot = RuntimeUtil.FindAvatarTransformInParents(component.transform);
        }

        public bool Equals(IVertexFilter other)
        {
            return other is VertexFilterByBone filter
                   && filter._bone == _bone
                   && Mathf.Approximately(filter._threshold, _threshold);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(typeof(VertexFilterByBone), _bone, _threshold);
        }

        public override string ToString()
        {
            return $"VertexFilterByBone: {(_bone != null ? _bone.name : "null")} @ {_threshold}";
        }

        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            if (_bone == null || !(renderer is SkinnedMeshRenderer smr))
            {
                return; // No bone specified or not a skinned mesh
            }

            // Find all bone indices in the renderer's bones array that match our target bone
            var boneIndices = new List<int>();
            var targetBoneRef = _bone != null ? ObjectRegistry.GetReference(_bone) : null;
            
            if (targetBoneRef == null)
            {
                return; // Cannot get reference for the target bone
            }
            
            for (int i = 0; i < smr.bones.Length; i++)
            {
                if (smr.bones[i] != null &&
                    (ObjectRegistry.GetReference(smr.bones[i]) == targetBoneRef || smr.bones[i] == _bone))
                {
                    boneIndices.Add(i);
                }
            }

            if (boneIndices.Count == 0)
            {
                return; // Bone not found in the renderer's bones
            }

            // Get bone weight data from the mesh
            var boneWeights = mesh.GetAllBoneWeights();
            var bonesPerVertex = mesh.GetBonesPerVertex();

            if (bonesPerVertex.Length == 0 || boneWeights.Length == 0)
            {
                return; // No bone weight data
            }

            if (bonesPerVertex.Length != filtered.Length)
            {
                throw new ArgumentException("Mesh vertex count does not match filtered array length.");
            }

            // Process each vertex's bone weights
            int weightIndex = 0;
            for (int vertexIndex = 0; vertexIndex < bonesPerVertex.Length; vertexIndex++)
            {
                int numWeights = bonesPerVertex[vertexIndex];

                if (numWeights == 0) continue;
                
                // Check if any weight for this vertex matches our target bone and exceeds threshold
                float totalWeight = 0;
                float targetWeight = 0;
                for (int weightOffset = 0; weightOffset < numWeights; weightOffset++)
                {
                    var boneWeight = boneWeights[weightIndex + weightOffset];
                    if (boneIndices.Contains(boneWeight.boneIndex))
                    {
                        targetWeight += boneWeight.weight;
                    }

                    totalWeight += boneWeight.weight;
                }

                if (targetWeight > 0 && targetWeight / totalWeight >= _threshold)
                {
                    filtered[vertexIndex] = true;
                }
                
                weightIndex += numWeights;
            }

        }
    }
}