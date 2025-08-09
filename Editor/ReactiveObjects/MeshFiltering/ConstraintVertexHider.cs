
#if MA_VRCSDK3_AVATARS
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32.SafeHandles;
using nadena.dev.ndmf.animator;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Dynamics.Constraint.Components;
using Object = System.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    using NDMFBuildContext = nadena.dev.ndmf.BuildContext;

    public class ConstraintVertexHider
    {
        struct InfluenceSet
        {
            public HashSet<string> targets;

            public override bool Equals(object obj)
            {
                if (obj is not InfluenceSet other)
                    return false;
                
                return targets.SetEquals(other.targets);
            }
            
            public override int GetHashCode()
            {
                return targets.Aggregate(0, (accum, s) => accum ^ s.GetHashCode());
            }
        }

        public struct HiderClips
        {
            public VirtualClip onHidden;
            public Action<VirtualClip> registerBaseStates;
        }

        private readonly NDMFBuildContext _context;
        private readonly SkinnedMeshRenderer _renderer;
        private readonly Mesh _originalMesh;
        private readonly Mesh _mesh;
        private readonly List<HashSet<string>> _vertexToTargets;

        private ConstraintVertexHider(NDMFBuildContext context, SkinnedMeshRenderer renderer, Mesh originalMesh,
            Mesh mesh, List<HashSet<string>> vertexToTargets)
        {
            this._context = context;
            this._renderer = renderer;
            this._originalMesh = originalMesh;
            this._mesh = mesh;
            this._vertexToTargets = vertexToTargets;
        }

        public static Dictionary<string, HiderClips> GenerateConstrainthider(
            NDMFBuildContext context,
            SkinnedMeshRenderer renderer,
            ref Mesh mesh,
            List<(string, float)> targetShapeNames
        )
        {
            var originalMesh = mesh;
            mesh = new Mesh();
            mesh.name = originalMesh.name + "_ConstraintHider";
            
            return new ConstraintVertexHider(context, renderer, originalMesh, mesh, VertexToTargets(originalMesh, targetShapeNames)).Generate();
        }

        private static List<HashSet<string>> VertexToTargets(
            Mesh originalMesh,
            List<(string, float)> shapeToThreshold
        )
        {
            List<HashSet<string>> vertexToTargets = new(originalMesh.vertexCount);
            
            for (int i = 0; i < originalMesh.vertexCount; i++)
            {
                vertexToTargets.Add(new HashSet<string>());
            }

            Vector3[] deltaPos = new Vector3[originalMesh.vertexCount];
            
            foreach (var (shapeName, shapeWeight) in shapeToThreshold)
            {
                var sqrThreshold = shapeWeight * shapeWeight;
                var shapeIndex = originalMesh.GetBlendShapeIndex(shapeName);
                if (shapeIndex < 0) continue;
                
                var frameCount = originalMesh.GetBlendShapeFrameCount(shapeIndex);
                
                for (int f = 0; f < frameCount; f++)
                {
                    originalMesh.GetBlendShapeFrameVertices(shapeIndex, f, deltaPos, null, null);
                    
                    for (int v = 0; v < originalMesh.vertexCount; v++)
                    {
                        var delta = deltaPos[v];
                        if (delta.sqrMagnitude < sqrThreshold) continue;
                        
                        vertexToTargets[v].Add(shapeName);
                    }
                }
            }

            return vertexToTargets;
        }

        private Dictionary<InfluenceSet, List<Transform>> _createdBones = new();
        private Dictionary<(InfluenceSet, int), int> _boneRemappings = new();
        private Dictionary<(InfluenceSet, int), int> _vertexRemappings = new();
        private List<Transform> _bones;
        private List<int> _vertToOriginalVert = new();
        private List<InfluenceSet> _vertInfluenceSets = new();
        
        private Dictionary<string, HiderClips> Generate()
        {
            _bones = _renderer.bones.ToList();
            List<List<int>> submeshes = TransformIndexBuffers();
            
            TransferVertexData();
            TransferBindPoses();
            WriteIndexBuffer(submeshes);
            _mesh.bounds = _originalMesh.bounds;
            _renderer.bones = _bones.ToArray();
            // TODO: transfer blend shapes

            return new();
        }

        private void WriteIndexBuffer(List<List<int>> submeshes)
        {
            _mesh.SetIndexBufferParams(
                submeshes.Select(l => l.Count).Sum(),
                IndexFormat.UInt32
            );
            
            int index = 0;
            List<SubMeshDescriptor> subMeshDescriptors = new();
            for (int i = 0; i < submeshes.Count; i++)
            {
                var indices = submeshes[i];
                if (indices.Count == 0) continue;

                var desc = _originalMesh.GetSubMesh(i);
                desc.indexCount = indices.Count;
                desc.indexStart = index;
                desc.baseVertex = 0;
                desc.firstVertex = indices.Min();
                desc.vertexCount = indices.Max() + 1 - desc.firstVertex;
                subMeshDescriptors.Add(new SubMeshDescriptor(index, indices.Count, _originalMesh.GetSubMesh(i).topology));
                _mesh.SetIndexBufferData(indices, 0, index, indices.Count, 
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds);
            }
            
            _mesh.subMeshCount = submeshes.Count;
            _mesh.SetSubMeshes(subMeshDescriptors);
        }

        private void TransferBindPoses()
        {
            var bindposes = _originalMesh.bindposes.ToList();

            foreach (var ((_, originalIndex), newIndex) in _boneRemappings.OrderBy(kv => kv.Value))
            {
                if (newIndex != bindposes.Count)
                {
                    throw new InvalidOperationException("Bind pose remapping does not match bone count.");
                }
                
                bindposes.Add(bindposes[originalIndex]);
            }
            
            _mesh.bindposes = bindposes.ToArray();
        }

        private int TopologyToVertexCount(MeshTopology topo)
        {
            switch (topo)
            {
                case MeshTopology.Lines: return 2;
                case MeshTopology.LineStrip: return 1;
                case MeshTopology.Triangles: return 3;
                case MeshTopology.Quads: return 4;
                case MeshTopology.Points: return 1;
                default: throw new ArgumentOutOfRangeException(nameof(topo), topo, null);
            }
        }
        
        private List<List<int>> TransformIndexBuffers()
        {
            var submeshes = new List<List<int>>();

            List<int> originalIndices = new List<int>();
            for (int sm = 0; sm < _originalMesh.subMeshCount; sm++)
            {
                List<int> submeshIndices = new List<int>();
                
                var smDesc = _originalMesh.GetSubMesh(sm);
                originalIndices.Clear();
                _originalMesh.GetIndices(originalIndices, sm, true);

                var vertsPerElem = TopologyToVertexCount(smDesc.topology);
                var topoElemCount = originalIndices.Count / vertsPerElem;
                
                for (int i = 0; i < topoElemCount; i++)
                {
                    var baseVert = i * vertsPerElem;
                    HashSet<string> mergedInfluences = new HashSet<string>();

                    for (int j = 0; j < vertsPerElem; j++)
                    {
                        mergedInfluences.UnionWith(_vertexToTargets[originalIndices[baseVert + j]]);
                    }

                    for (int j = 0; j < vertsPerElem; j++)
                    {
                        int originalVertex = originalIndices[baseVert + j];
                        int newVertex = TransformVertex(originalVertex, mergedInfluences);
                        submeshIndices.Add(newVertex);
                    }
                }
                
                submeshes.Add(submeshIndices);
            }

            return submeshes;
        }

        int TransformVertex(int originalVertex, HashSet<string> mergedInfluences)
        {
            InfluenceSet influenceSet = new() { targets = mergedInfluences };

            if (_vertexRemappings.TryGetValue((influenceSet, originalVertex), out var newVertex))
            {
                return newVertex;
            }
            
            newVertex = _vertToOriginalVert.Count;
            _vertToOriginalVert.Add(originalVertex);
            _vertInfluenceSets.Add(influenceSet);
            _vertexRemappings[(influenceSet, originalVertex)] = newVertex;

            return newVertex;
        }

        private int DescriptorSize(VertexAttributeDescriptor vad)
        {
            int formatLen;
            switch (vad.format)
            {
                case VertexAttributeFormat.Float16: formatLen = 2; break;
                case VertexAttributeFormat.Float32: formatLen = 4; break;
                case VertexAttributeFormat.SInt8: formatLen = 1; break;
                case VertexAttributeFormat.SInt16: formatLen = 2; break;
                case VertexAttributeFormat.SInt32: formatLen = 4; break;
                case VertexAttributeFormat.SNorm8: formatLen = 1; break;
                case VertexAttributeFormat.SNorm16: formatLen = 2; break;
                case VertexAttributeFormat.UInt8: formatLen = 1; break;
                case VertexAttributeFormat.UInt16: formatLen = 2; break;
                case VertexAttributeFormat.UInt32: formatLen = 4; break;
                case VertexAttributeFormat.UNorm8: formatLen = 1; break;
                case VertexAttributeFormat.UNorm16: formatLen = 2; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(vad.format), vad.format, null);
            }

            return formatLen * vad.dimension;
        }
        
        void TransferVertexData()
        {
            var format = _originalMesh.GetVertexAttributes();
            Debug.Log($"Format: {string.Join(", ", format.Select(d => $"{d.attribute} ({d.stream})"))}");
            
            _mesh.SetVertexBufferParams(_vertToOriginalVert.Count, format);
            
            TransferStream(0);
            TransferStream(1);
            TransferStream(2);
            TransferStream(3);

            var origVerts = _originalMesh.vertices;
            var destVerts = _mesh.vertices;
            int limit = 100;
            for (int i = 0; i < _vertToOriginalVert.Count; i++)
            {
                var srcVertPos = origVerts[_vertToOriginalVert[i]];
                var dstVertPos = destVerts[i];

                if (srcVertPos != dstVertPos)
                {
                    Debug.Log($"Mismatch at vertex {i}: original index {_vertToOriginalVert[i]} pos {srcVertPos} != new {dstVertPos}");
                    if (limit-- == 0) break;
                }
            }

            // Disable to isolate the issue
            UpdateBoneIndices();

            void TransferStream(int streamIndex)
            {
                if (format.All(attr => attr.stream != streamIndex))
                {
                    return;
                }
                
                var originalBuffer = _originalMesh.GetVertexBuffer(streamIndex);
                var elementSize = originalBuffer.stride;
                byte[] sourceData = new byte[elementSize * originalBuffer.count];
                originalBuffer.GetData(sourceData);
                Debug.Log("Stream " + streamIndex + " original buffer count: " + originalBuffer.count + " stride: " + originalBuffer.stride + " vertices: " + _originalMesh.vertexCount);

                var destData = new byte[elementSize * _vertexRemappings.Count];

                int dstIndex = 0;
                foreach (var originalVertex in _vertToOriginalVert)
                {
                    int originalStart = originalVertex * elementSize;

                    if (dstIndex == 90 * elementSize)
                    {
                        Debug.Log($"Cloning index 90 to position " + dstIndex + " from original vertex " + originalVertex + " at start " + originalStart);
                    }
                    
                    Array.Copy(sourceData, originalStart, destData, dstIndex, elementSize);
                    dstIndex += elementSize;
                }
                Debug.Log($"Copy ended at dstIndex {dstIndex} for stream {streamIndex} with element size {elementSize} and vertex count {_vertexRemappings.Count}");
                
                _mesh.SetVertexBufferData(destData, 0, 0, _vertexRemappings.Count * elementSize, streamIndex);
            }
        }

        private void UpdateBoneIndices()
        {
            //TODO: did we copy the bones in TransferStream?
            var originalBonesPerVertex = _originalMesh.GetBonesPerVertex();
            var originalBoneWeightBuffer = _originalMesh.GetAllBoneWeights();

            var bonesPerVertex = new NativeArray<byte>(_vertInfluenceSets.Count, Allocator.Temp);
            var boneWeightTempBuffer = new List<BoneWeight1>();

            var originalStartingIndex = new int[bonesPerVertex.Length];

            int boneOffset = 0;
            for (int i = 0; i < originalBonesPerVertex.Length; i++)
            {
                originalStartingIndex[i] = boneOffset;
                boneOffset += (int)originalBonesPerVertex[i];
            }
            
            for (int i = 0; i < _vertToOriginalVert.Count; i++)
            {
                var originalVertex = _vertToOriginalVert[i];
                
                bonesPerVertex[i] = originalBonesPerVertex[originalVertex];
                for (int b = 0; b < originalBonesPerVertex[originalVertex]; b++)
                {
                    var boneWeight = originalBoneWeightBuffer[originalStartingIndex[originalVertex] + b];
                    var originalBoneIndex = boneWeight.boneIndex;
                    if (originalBoneIndex < 0) continue;

                    // Find the influence set for this vertex
                    var influenceSet = _vertInfluenceSets[i];
                    var newBone = MapBoneIndex(originalBoneIndex, influenceSet);
                    boneWeight.boneIndex = newBone;
                    boneWeightTempBuffer.Add(boneWeight);
                }
            }
            
            var boneWeightBuffer = new NativeArray<BoneWeight1>(boneWeightTempBuffer.Count, Allocator.Temp);
            for (int i = 0; i < boneWeightTempBuffer.Count; i++)
            {
                boneWeightBuffer[i] = boneWeightTempBuffer[i];
            }
            
            _mesh.SetBoneWeights(bonesPerVertex, boneWeightBuffer);

            bonesPerVertex.Dispose();
            boneWeightBuffer.Dispose();
        }

        private int MapBoneIndex(int originalBoneIndex, InfluenceSet influenceSet)
        {
            if (influenceSet.targets.Count == 0)
            {
                return originalBoneIndex;
            }
            
            if (_boneRemappings.TryGetValue((influenceSet, originalBoneIndex), out var newBoneIndex))
            {
                return newBoneIndex;
            }

            if (originalBoneIndex < 0 || originalBoneIndex >= _bones.Count)
            {
                return -1;
            }
            
            Transform originalBone = _bones[originalBoneIndex];
            if (originalBone == null) return originalBoneIndex;

            var subBoneName = "__MA_MeshCutter_Constraint_" + _renderer.GetInstanceID() + "_" + originalBoneIndex;
            var subBone = new GameObject(subBoneName).transform;
            subBone.SetParent(originalBone, false);
                
            var parentConstraint = subBone.gameObject.AddComponent<VRCParentConstraint>();
            parentConstraint.PositionAtRest = Vector3.zero;
            parentConstraint.GlobalWeight = 0;
                
            parentConstraint.Sources.Add(new()
            {
                SourceTransform = _context.AvatarRootTransform,
                Weight = 1f
            });

            int index = _bones.Count;
                
            if (!_createdBones.TryGetValue(influenceSet, out var existingBoneList))
            {
                existingBoneList = new List<Transform>();
                _createdBones[influenceSet] = existingBoneList;
            }
            existingBoneList.Add(subBone);
                
            _boneRemappings[(influenceSet, originalBoneIndex)] = index;
            _bones.Add(subBone);
            
            return index;
        }
    }
}
#endif