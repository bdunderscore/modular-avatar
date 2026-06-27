using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class VisibleHeadAccessoryMeshProcessor
    {
        private SkinnedMeshRenderer _renderer;
        private HashSet<Transform> _visibleBones;
        private BuildContext _context;

        public List<Transform> HeadChopTargets { get; } = new List<Transform>();

        public VisibleHeadAccessoryMeshProcessor(
            SkinnedMeshRenderer renderer,
            HashSet<Transform> visibleBones,
            BuildContext context
        )
        {
            _renderer = renderer;
            _visibleBones = visibleBones;
            _context = context;
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

            // Step 2: find mixed primitives → mark vertices with non-visible weights for cloning
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

            // These transfer calls invalidate the native arrays we obtained above, so reacquire them
            boneWeights = originalMesh.GetAllBoneWeights();
            bonesPerVertex = originalMesh.GetBonesPerVertex();

            // Step 5: build bone weight arrays (preallocated, written directly)
            var newWeights = new BoneWeight1[totalBoneWeightCount];
            var newBonesPerVertex = new byte[newVertexCount];

            var bindposes = new List<Matrix4x4>();
            originalMesh.GetBindposes(bindposes);
            var bones = new List<Transform>(originalBones);
            var proxyIndices = new Dictionary<int, int>();

            var weightIdx = 0;
            src_w_base = 0;
            for (int v = 0; v < vertexCount; v++)
            {
                int count = bonesPerVertex[v];
                newBonesPerVertex[v] = (byte)count;
                for (var w = 0; w < count; w++)
                    newWeights[weightIdx++] = boneWeights[src_w_base + w];
                src_w_base += count;
            }

            // Clones: copy original weights, remap non-visible bone indices
            for (var v = vertexCount; v < newVertexCount; v++)
            {
                var origV = newToOrig[v];
                var origBase = 0;
                for (var i = 0; i < origV; i++) origBase += bonesPerVertex[i];

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
            // Step 6: rebuild index buffers using clone refs for mixed primitives
            for (var s = 0; s < originalMesh.subMeshCount; s++)
            {
                var indices = originalMesh.GetIndices(s);
                var topology = originalMesh.GetTopology(s);
                var stride = PrimitiveStride(topology);
                var count = indices.Length;

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

                    newMesh.SetIndices(newIndices, topology, s, false);
                }
                else
                {
                    newMesh.SetIndices(indices, topology, s, false);
                }
            }

            _renderer.bones = bones.ToArray();
            _renderer.sharedMesh = newMesh;

            int RemapBone(int originalIndex)
            {
                if (proxyIndices.TryGetValue(originalIndex, out var index)) return index;

                var originalBone = originalBones[originalIndex];

                var clone = new GameObject(originalBone.name + " (VHA Clone)");
                clone.transform.SetParent(originalBone, false);

                HeadChopTargets.Add(clone.transform);

                index = bones.Count;
                bones.Add(clone.transform);
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