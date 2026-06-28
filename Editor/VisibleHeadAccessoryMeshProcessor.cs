using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class VisibleHeadAccessoryMeshProcessor
    {
        private SkinnedMeshRenderer _renderer;
        private HashSet<Transform> _visibleBones;
        private BuildContext _context;
        private readonly Func<Transform, Transform> _boneRemap;

        public VisibleHeadAccessoryMeshProcessor(
            SkinnedMeshRenderer renderer,
            HashSet<Transform> visibleBones,
            BuildContext context,
            Func<Transform, Transform> boneRemap
        )
        {
            _renderer = renderer;
            _visibleBones = visibleBones;
            _context = context;
            _boneRemap = boneRemap;
        }

        public bool NeedsRetargeting()
        {
            return _renderer.bones.Any(_visibleBones.Contains) && _renderer.bones.Any(b => !_visibleBones.Contains(b));
        }

        public void Retarget()
        {
            if (!NeedsRetargeting()) return;

            var originalMesh = _renderer.sharedMesh;
            var vertexCount = originalMesh.vertexCount;
            var boneWeights = originalMesh.GetAllBoneWeights();
            var bonesPerVertex = originalMesh.GetBonesPerVertex();
            var originalBones = _renderer.bones;

            // Step 1: classify each vertex
            var anyVisible = false;
            var hasVisible = new bool[vertexCount];
            var hasNonVisible = new bool[vertexCount];
            var src_w_base = 0;
            for (var v = 0; v < vertexCount; v++)
            {
                int count = bonesPerVertex[v];
                for (var w = 0; w < count; w++)
                {
                    var bw = boneWeights[src_w_base + w];
                    var bone = originalBones[bw.boneIndex];
                    if (bone == null) continue;
                    if (_visibleBones.Contains(bone))
                    {
                        hasVisible[v] = true;
                        anyVisible = true;
                    }
                    else
                    {
                        hasNonVisible[v] = true;
                    }
                }
                src_w_base += count;
            }
            if (!anyVisible) return;

            // Step 2: find mixed primitives -> mark vertices with non-visible weights for cloning
            var needsClone = new bool[vertexCount];
            var cloneCount = 0;

            for (var s = 0; s < originalMesh.subMeshCount; s++)
            {
                var indices = originalMesh.GetIndices(s);
                var topology = originalMesh.GetTopology(s);
                var stride = PrimitiveStride(topology);
                if (stride == 0) continue;

                for (var t = 0; t < indices.Length; t += stride)
                {
                    var primHasVisible = false;
                    var primHasNonVisible = false;
                    for (var i = 0; i < stride; i++)
                    {
                        if (hasVisible[indices[t + i]]) primHasVisible = true;
                        if (hasNonVisible[indices[t + i]]) primHasNonVisible = true;
                    }
                    if (!primHasVisible || !primHasNonVisible) continue;

                    for (var i = 0; i < stride; i++)
                    {
                        var vIdx = indices[t + i];
                        if (hasNonVisible[vIdx] && !needsClone[vIdx])
                        {
                            needsClone[vIdx] = true;
                            cloneCount++;
                        }
                    }
                }
            }

            // Pre-calculate total bone weight count for preallocation
            var totalBoneWeightCount = 0;
            for (var v = 0; v < vertexCount; v++)
            {
                totalBoneWeightCount += bonesPerVertex[v];
                if (needsClone[v]) totalBoneWeightCount += bonesPerVertex[v];
            }
            if (cloneCount == 0) return;

            // Step 3: build new-to-original mapping
            var newVertexCount = vertexCount + cloneCount;
            var newToOrig = new int[newVertexCount];
            var origToClone = new Dictionary<int, int>();

            for (var v = 0; v < vertexCount; v++)
                newToOrig[v] = v;

            var nextClone = vertexCount;
            for (var v = 0; v < vertexCount; v++)
            {
                if (needsClone[v])
                {
                    newToOrig[nextClone] = v;
                    origToClone[v] = nextClone;
                    nextClone++;
                }
            }

            // Step 4: create new mesh, copy vertex data
            var newMesh = Object.Instantiate(originalMesh);
            _context.SaveAsset(newMesh);
            MeshVertexCopyUtil.TransferVertexData(newMesh, originalMesh, newToOrig);
            MeshVertexCopyUtil.TransferShapes(newMesh, originalMesh, newToOrig);

            // Instantiate shares internal buffers; SetVertexBufferData on the copy invalidates
            // the original mesh's native arrays. Reacquire before step 5.
            boneWeights = originalMesh.GetAllBoneWeights();
            bonesPerVertex = originalMesh.GetBonesPerVertex();

            // Step 5: build bone weight arrays (preallocated, written directly)
            var newWeights = new BoneWeight1[totalBoneWeightCount];
            var newBonesPerVertex = new byte[newVertexCount];

            var bindposes = new List<Matrix4x4>();
            originalMesh.GetBindposes(bindposes);
            var bones = new List<Transform>(originalBones);
            var proxyIndices = new Dictionary<int, int>();

            // Precompute cumulative bone-weight start offsets for O(1) random access
            var boneWeightStart = new int[vertexCount];
            var running = 0;
            for (var v = 0; v < vertexCount; v++)
            {
                boneWeightStart[v] = running;
                running += bonesPerVertex[v];
            }

            var weightIdx = 0;
            for (int v = 0; v < vertexCount; v++)
            {
                int count = bonesPerVertex[v];
                newBonesPerVertex[v] = (byte)count;
                for (var w = 0; w < count; w++)
                    newWeights[weightIdx++] = boneWeights[boneWeightStart[v] + w];
            }

            // Clones: copy original weights, remap non-visible bone indices
            for (var v = vertexCount; v < newVertexCount; v++)
            {
                var origV = newToOrig[v];
                var origBase = boneWeightStart[origV];

                int count = bonesPerVertex[origV];
                newBonesPerVertex[v] = (byte)count;
                for (var w = 0; w < count; w++)
                {
                    var bw = boneWeights[origBase + w];
                    var bone = originalBones[bw.boneIndex];
                    if (bone != null && !_visibleBones.Contains(bone))
                        bw.boneIndex = RemapBone(bw.boneIndex);
                    newWeights[weightIdx++] = bw;
                }
            }

            using (var bpvNative = new NativeArray<byte>(newBonesPerVertex, Allocator.Temp))
            using (var nativeWeights = new NativeArray<BoneWeight1>(newWeights, Allocator.Temp))
            {
                newMesh.SetBoneWeights(bpvNative, nativeWeights);
            }
            newMesh.bindposes = bindposes.ToArray();

            // Step 6: rebuild index buffers using clone refs for mixed primitives.
            // Build all buffers first, then determine if UInt32 format is needed.
            var subMeshCount = originalMesh.subMeshCount;
            var newIndexArrays = new int[subMeshCount][];
            var newTopologies = new MeshTopology[subMeshCount];

            for (var s = 0; s < subMeshCount; s++)
            {
                var indices = originalMesh.GetIndices(s);
                var topology = originalMesh.GetTopology(s);
                var stride = PrimitiveStride(topology);
                var count = indices.Length;
                newTopologies[s] = topology;

                if (stride > 0)
                {
                    var newIndices = new int[count];
                    for (var t = 0; t < count; t += stride)
                    {
                        var primHasVisible = false;
                        for (var i = 0; i < stride; i++)
                            if (hasVisible[indices[t + i]])
                            {
                                primHasVisible = true;
                                break;
                            }

                        for (var i = 0; i < stride; i++)
                        {
                            var origIdx = indices[t + i];
                            if (primHasVisible && hasNonVisible[origIdx])
                                newIndices[t + i] = origToClone[origIdx];
                            else
                                newIndices[t + i] = origIdx;
                        }
                    }

                    newIndexArrays[s] = newIndices;
                }
                else
                {
                    newIndexArrays[s] = indices;
                }
            }

            // Check if all submeshes fit within UInt16 range (span < 65536)
            // using baseVertex per submesh.
            var needsUInt32 = false;
            var baseVertices = new int[subMeshCount];

            for (var s = 0; s < subMeshCount; s++)
            {
                var buf = newIndexArrays[s];
                if (buf.Length == 0) continue;

                var min = buf[0];
                var max = buf[0];
                for (var i = 1; i < buf.Length; i++)
                {
                    var idx = buf[i];
                    if (idx < min) min = idx;
                    if (idx > max) max = idx;
                }

                if (max - min >= 65536)
                {
                    needsUInt32 = true;
                    break;
                }

                baseVertices[s] = min;
            }

            // Always rebase indices to use baseVertex — harmless for UInt32, required for UInt16.
            for (var s = 0; s < subMeshCount; s++)
            {
                var buf = newIndexArrays[s];
                var bv = baseVertices[s];
                if (bv > 0)
                {
                    for (var i = 0; i < buf.Length; i++)
                        buf[i] -= bv;
                }
            }

            newMesh.indexFormat = needsUInt32
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            for (var s = 0; s < subMeshCount; s++)
                newMesh.SetIndices(newIndexArrays[s], newTopologies[s], s, false, baseVertices[s]);

            _renderer.bones = bones.ToArray();
            _renderer.sharedMesh = newMesh;

            int RemapBone(int originalIndex)
            {
                if (proxyIndices.TryGetValue(originalIndex, out var index)) return index;

                var originalBone = originalBones[originalIndex];
                var clone = _boneRemap(originalBone);

                index = bones.Count;
                bones.Add(clone);
                bindposes.Add(bindposes[originalIndex]);

                proxyIndices[originalIndex] = index;
                return index;
            }

            static int PrimitiveStride(MeshTopology t)
            {
                return t switch
                {
                    MeshTopology.Triangles => 3,
                    MeshTopology.Quads => 4,
                    _ => 0
                };
            }
        }
    }
}