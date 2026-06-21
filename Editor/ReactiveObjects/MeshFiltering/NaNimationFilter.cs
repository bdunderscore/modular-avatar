using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Manipulates the bone weights of a mesh to prepare to hide primitives using "NaNimation" (as originally described
    ///     by d4rk in https://github.com/d4rkc0d3r/d4rkAvatarOptimizer?tab=readme-ov-file#nanimation-toggles ).
    ///     NaNimation works by animating the scale of bones to NaN, in order to cause any primitives including those
    ///     vertices to be hidden. This allows for arbitrary sections of the mesh to be hidden without needing specific
    ///     shader support.
    /// </summary>
    internal static class NaNimationFilter
    {
        public const string NaNimatedBufferPrefix = "NaNimation buffer";
        public const string NaNimatedBonePrefix = "NaNimatedBone for ";

        public struct AddedBone
        {
            public int originalBoneIndex;
            public int newBoneIndex;
        }

        // ── Hide-key types ────────────────────────────────────────────────────────

        private interface IHideKey<TSelf> : IEquatable<TSelf>
        {
            bool Contains(int shapeIdx);
            bool IsEmpty { get; }
        }

        /// <summary>Fast path: up to 64 shapes represented as a bitmask.</summary>
        private readonly struct SmallHideKey : IHideKey<SmallHideKey>
        {
            private readonly ulong _bits;

            private SmallHideKey(ulong bits)
            {
                _bits = bits;
            }

            public static SmallHideKey From(List<int> shapeIndices)
            {
                ulong bits = 0;
                foreach (var s in shapeIndices) bits |= 1UL << s;
                return new SmallHideKey(bits);
            }

            public bool Contains(int s)
            {
                return s >= 0 && s < 64 && ((_bits >> s) & 1) != 0;
            }

            public bool IsEmpty => _bits == 0;

            public bool Equals(SmallHideKey other)
            {
                return _bits == other._bits;
            }

            public override bool Equals(object obj)
            {
                return obj is SmallHideKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _bits.GetHashCode();
            }
        }

        /// <summary>Fallback for more than 64 shapes: sorted int array + string key for hashing.</summary>
        private readonly struct LargeHideKey : IHideKey<LargeHideKey>
        {
            private readonly string _key;
            private readonly int[] _sortedShapes;

            private LargeHideKey(string key, int[] sortedShapes)
            {
                _key = key;
                _sortedShapes = sortedShapes;
            }

            public static LargeHideKey From(List<int> shapeIndices)
            {
                // shapeIndices is already sorted (built by iterating s = 0..N-1)
                var arr = shapeIndices.ToArray();
                return new LargeHideKey(string.Join(",", arr), arr);
            }

            public bool Contains(int s)
            {
                return _sortedShapes != null && Array.BinarySearch(_sortedShapes, s) >= 0;
            }

            public bool IsEmpty => string.IsNullOrEmpty(_key);

            public bool Equals(LargeHideKey other)
            {
                return _key == other._key;
            }

            public override bool Equals(object obj)
            {
                return obj is LargeHideKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _key?.GetHashCode() ?? 0;
            }
        }

        // ── Public entry point ────────────────────────────────────────────────────

        internal static Dictionary<(TargetProp, IMeshSelector), List<AddedBone>> ComputeNaNPlan(
            Renderer renderer,
            ref Mesh mesh,
            List<(TargetProp, IMeshSelector)> targets
        )
        {
            if (mesh.vertexCount == 0 || targets.Count == 0)
                return new Dictionary<(TargetProp, IMeshSelector), List<AddedBone>>();

            if (targets.Count <= 64)
                return ComputeNaNPlanInner(renderer, ref mesh, targets, SmallHideKey.From);
            else
                return ComputeNaNPlanInner(renderer, ref mesh, targets, LargeHideKey.From);
        }

        // ── Core algorithm ────────────────────────────────────────────────────────

        private static Dictionary<(TargetProp, IMeshSelector), List<AddedBone>>
            ComputeNaNPlanInner<THideKey>(
                Renderer renderer,
                ref Mesh mesh,
                List<(TargetProp, IMeshSelector)> targets,
                Func<List<int>, THideKey> makeHideKey
            ) where THideKey : struct, IHideKey<THideKey>
        {
            var originalVertexCount = mesh.vertexCount;
            var submeshCount = mesh.subMeshCount;

            var primitiveMasks = new NativeArray<bool>[targets.Count][];
            for (var s = 0; s < targets.Count; s++)
                primitiveMasks[s] = new NativeArray<bool>[submeshCount];

            NativeArray<BoneWeight1> extBW = default;
            NativeArray<byte> extBpV = default;

            try
            {
                // ── Phase A: Collect primitive masks ──────────────────────────────

                var vertsPerPrim = new int[submeshCount];
                int[][] origIndexBuffers;

                using (var selectorJob = new MeshSelectorJob(renderer, mesh))
                {
                    var handles = new JobHandle[targets.Count * submeshCount];

                    for (var s = 0; s < targets.Count; s++)
                    {
                        var selector = targets[s].Item2;
                        for (var sm = 0; sm < submeshCount; sm++)
                        {
                            var desc = selectorJob.MeshData.GetSubMesh(sm);
                            vertsPerPrim[sm] = VertsPerPrim(desc.topology);
                            var primCount = desc.indexCount / vertsPerPrim[sm];
                            primitiveMasks[s][sm] = new NativeArray<bool>(primCount, Allocator.TempJob);
                            handles[s * submeshCount + sm] = selector.MarkFilteredPrimitives(
                                selectorJob, sm, primitiveMasks[s][sm]);
                        }
                    }

                    // Complete all primitive mask jobs before reading their results
                    foreach (var h in handles) h.Complete();

                    origIndexBuffers = new int[submeshCount][];
                    for (var sm = 0; sm < submeshCount; sm++)
                    {
                        var (buf, dep) = selectorJob.GetSubmeshIndexBuffer(sm);
                        dep.Complete();
                        origIndexBuffers[sm] = buf.ToArray();
                    }
                } // selectorJob disposed

                // ── Phase B+C: Compute hide keys & build vertex duplication plan ──

                var newToOrig = new List<int>(originalVertexCount + 16);
                for (var i = 0; i < originalVertexCount; i++) newToOrig.Add(i);

                var cloneHideKeys = new List<THideKey>(); // index = (newVertIdx - originalVertexCount)
                var cloneMap = new Dictionary<(int, THideKey), int>();

                var newIndexBuffers = new int[submeshCount][];
                for (var sm = 0; sm < submeshCount; sm++)
                    newIndexBuffers[sm] = (int[])origIndexBuffers[sm].Clone();

                // Pre-compute single-shape hide keys for the heuristic (avoids per-iteration allocation)
                var singleShapeKeys = new THideKey[targets.Count];
                var scratch = new List<int>(1) { 0 };
                for (var s = 0; s < targets.Count; s++)
                {
                    scratch[0] = s;
                    singleShapeKeys[s] = makeHideKey(scratch);
                }

                var shapeIndicesScratch = new List<int>(targets.Count);

                for (var sm = 0; sm < submeshCount; sm++)
                {
                    var vpp = vertsPerPrim[sm];
                    var primCount = origIndexBuffers[sm].Length / vpp;

                    for (var p = 0; p < primCount; p++)
                    {
                        // Build hide key: sorted set of shape indices that select this primitive
                        shapeIndicesScratch.Clear();
                        for (var s = 0; s < targets.Count; s++)
                            if (primitiveMasks[s][sm][p])
                                shapeIndicesScratch.Add(s);

                        if (shapeIndicesScratch.Count == 0) continue;

                        var hideKey = makeHideKey(shapeIndicesScratch);

                        // Check if any vertex in this primitive already has a clone for this hideKey
                        var existingCloneVert = -1;
                        var existingSlot = -1;
                        for (var vi = 0; vi < vpp; vi++)
                        {
                            var origVert = origIndexBuffers[sm][p * vpp + vi];
                            if (cloneMap.TryGetValue((origVert, hideKey), out var cloneVert))
                            {
                                existingCloneVert = cloneVert;
                                existingSlot = vi;
                                break;
                            }
                        }

                        if (existingCloneVert >= 0)
                        {
                            // Reuse existing clone — update only this slot
                            newIndexBuffers[sm][p * vpp + existingSlot] = existingCloneVert;
                        }
                        else
                        {
                            // Need to clone a vertex for this primitive.
                            // Heuristic: prefer a slot that already has a clone for any shape in hideKey,
                            // so ComputeNaNPlanForShape can build on an existing NaN bone redirect.
                            var chosenSlot = 0;
                            for (var vi = 0; vi < vpp; vi++)
                            {
                                var origVert = origIndexBuffers[sm][p * vpp + vi];
                                foreach (var s in shapeIndicesScratch)
                                {
                                    if (cloneMap.ContainsKey((origVert, singleShapeKeys[s])))
                                    {
                                        chosenSlot = vi;
                                        goto doneChoosingSlot;
                                    }
                                }
                            }

                            doneChoosingSlot:

                            var chosenOrigVert = origIndexBuffers[sm][p * vpp + chosenSlot];
                            var newVertIdx = newToOrig.Count;
                            newToOrig.Add(chosenOrigVert);
                            cloneHideKeys.Add(hideKey);
                            cloneMap[(chosenOrigVert, hideKey)] = newVertIdx;
                            newIndexBuffers[sm][p * vpp + chosenSlot] = newVertIdx;
                        }
                    }
                }

                // No primitives selected by any shape — leave mesh unchanged
                if (cloneMap.Count == 0) return new Dictionary<(TargetProp, IMeshSelector), List<AddedBone>>();

                var newVertCount = newToOrig.Count;
                var newToOrigArray = newToOrig.ToArray();

                // ── Phase D: Build new mesh ───────────────────────────────────────

                // Read bone weights from original BEFORE replacing mesh reference
                var origBoneWeights = mesh.GetAllBoneWeights();
                var origBonesPerVertex = mesh.GetBonesPerVertex();

                var originalMesh = mesh;
                mesh = new Mesh();
                mesh.name = originalMesh.name;
                mesh.indexFormat = originalMesh.indexFormat == IndexFormat.UInt32 || newVertCount > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
                mesh.bounds = originalMesh.bounds;
                ObjectRegistry.RegisterReplacedObject(originalMesh, mesh);

                MeshVertexCopyUtil.TransferVertexData(mesh, originalMesh, newToOrigArray);
                mesh.bindposes = originalMesh.bindposes;
                MeshVertexCopyUtil.TransferShapes(mesh, originalMesh, newToOrigArray);

                mesh.subMeshCount = submeshCount;
                for (var sm = 0; sm < submeshCount; sm++)
                    WriteSubmesh(mesh, sm, originalMesh.GetTopology(sm), newIndexBuffers[sm], mesh.indexFormat);

                // ── Phase E: Extend bone weights & compute NaN plan ───────────────

                var initialBoneCount = originalMesh.bindposeCount;
                var cloneCount = newVertCount - originalVertexCount;

                if (origBoneWeights.Length == 0)
                {
                    // Non-skinned mesh: create a synthetic bone 0 for every vertex
                    extBW = new NativeArray<BoneWeight1>(newVertCount, Allocator.Temp);
                    extBpV = new NativeArray<byte>(newVertCount, Allocator.Temp);
                    for (var v = 0; v < newVertCount; v++)
                    {
                        extBW[v] = new BoneWeight1 { boneIndex = 0, weight = 1f };
                        extBpV[v] = 1;
                    }

                    initialBoneCount = 1;
                    mesh.bindposes = new[] { Matrix4x4.identity };

                    if (renderer is SkinnedMeshRenderer smr)
                        smr.bones = new[] { smr.transform };
                }
                else
                {
                    // Skinned mesh: copy original weights, then append clone entries

                    // Build firstBoneIndex[] for original vertices to index into origBoneWeights
                    var origFirstBoneIndex = new int[originalVertexCount];
                    var ri = 0;
                    for (var v = 0; v < originalVertexCount; v++)
                    {
                        origFirstBoneIndex[v] = ri;
                        ri += origBonesPerVertex[v];
                    }

                    var extraBW = 0;
                    for (var ci = 0; ci < cloneCount; ci++)
                        extraBW += origBonesPerVertex[newToOrig[originalVertexCount + ci]];

                    extBW = new NativeArray<BoneWeight1>(origBoneWeights.Length + extraBW, Allocator.Temp);
                    extBpV = new NativeArray<byte>(newVertCount, Allocator.Temp);

                    for (var v = 0; v < originalVertexCount; v++)
                        extBpV[v] = origBonesPerVertex[v];
                    NativeArray<BoneWeight1>.Copy(origBoneWeights, 0, extBW, 0, origBoneWeights.Length);

                    var writeIdx = origBoneWeights.Length;
                    for (var ci = 0; ci < cloneCount; ci++)
                    {
                        var origV = newToOrig[originalVertexCount + ci];
                        var bpv = origBonesPerVertex[origV];
                        extBpV[originalVertexCount + ci] = bpv;
                        var srcBase = origFirstBoneIndex[origV];
                        for (var b = 0; b < bpv; b++)
                            extBW[writeIdx++] = origBoneWeights[srcBase + b];
                    }
                }

                // Build firstBoneIndex[] over all new vertices
                var firstBoneIndex = new int[newVertCount];
                {
                    var ri = 0;
                    for (var v = 0; v < newVertCount; v++)
                    {
                        firstBoneIndex[v] = ri;
                        ri += extBpV[v];
                    }
                }

                var nextBoneIndex = initialBoneCount;
                var result = new Dictionary<(TargetProp, IMeshSelector), List<AddedBone>>();
                var vertexMask = new bool[newVertCount];

                for (var s = 0; s < targets.Count; s++)
                {
                    Array.Fill(vertexMask, false);
                    for (var ci = 0; ci < cloneCount; ci++)
                    {
                        if (cloneHideKeys[ci].Contains(s))
                            vertexMask[originalVertexCount + ci] = true;
                    }

                    if (!vertexMask.Any(b => b)) continue;

                    var shapePlan = ComputeNaNPlanForShape(ref nextBoneIndex, extBW, extBpV,
                        firstBoneIndex, vertexMask);

                    if (shapePlan.Count > 0)
                        result.Add(targets[s], shapePlan);
                }

                // ── Phase F: Apply bone weights & bindposes ───────────────────────

                var bindposes = mesh.bindposes;
                Array.Resize(ref bindposes, nextBoneIndex);
                foreach (var addedBone in result.SelectMany(kv => kv.Value).OrderBy(b => b.originalBoneIndex))
                    bindposes[addedBone.newBoneIndex] = bindposes[addedBone.originalBoneIndex];
                mesh.bindposes = bindposes;
                mesh.SetBoneWeights(extBpV, extBW);

                return result;
            }
            finally
            {
                for (var s = 0; s < targets.Count; s++)
                for (var sm = 0; sm < submeshCount; sm++)
                    if (primitiveMasks[s][sm].IsCreated)
                        primitiveMasks[s][sm].Dispose();

                if (extBW.IsCreated) extBW.Dispose();
                if (extBpV.IsCreated) extBpV.Dispose();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void WriteSubmesh(Mesh mesh, int sm, MeshTopology topology, int[] flatIndexBuffer,
            IndexFormat indexFormat)
        {
            if (indexFormat == IndexFormat.UInt16)
            {
                var minVertex = flatIndexBuffer.Length > 0 ? flatIndexBuffer.Min() : 0;
                var indices16 = new ushort[flatIndexBuffer.Length];
                for (var i = 0; i < flatIndexBuffer.Length; i++)
                    indices16[i] = (ushort)(flatIndexBuffer[i] - minVertex);
                mesh.SetIndices(indices16, 0, indices16.Length, topology, sm, true, minVertex);
            }
            else
            {
                mesh.SetIndices(flatIndexBuffer, 0, flatIndexBuffer.Length, topology, sm);
            }
        }

        private static int VertsPerPrim(MeshTopology topology)
        {
            switch (topology)
            {
                case MeshTopology.Triangles: return 3;
                case MeshTopology.Quads: return 4;
                default: return 1;
            }
        }

        private static List<AddedBone> ComputeNaNPlanForShape(
            ref int nextBoneIndex,
            NativeArray<BoneWeight1> boneWeights,
            NativeArray<byte> boneCounts,
            int[] firstBoneIndex,
            bool[] vertexMask
        )
        {
            var boneToVertexCount = new Dictionary<int, int>();
            var remainingVertices = new List<int>();

            var vertCount = vertexMask.Length;
            for (int v = 0; v < vertCount; v++)
            {
                if (!vertexMask[v]) continue;

                remainingVertices.Add(v);
                for (var bi = 0; bi < boneCounts[v]; bi++)
                {
                    var boneWeight = boneWeights[bi + firstBoneIndex[v]];
                    if (boneWeight.weight == 0) continue; // we're avoiding actual floating point zero here specifically
                    if (boneWeight.boneIndex < 0) continue;

                    var count = boneToVertexCount.GetValueOrDefault(boneWeight.boneIndex, 0);
                    boneToVertexCount[boneWeight.boneIndex] = count + 1;
                }
            }

            var sortedBones = boneToVertexCount
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

            var shapePlan = new List<AddedBone>();

            while (remainingVertices.Count > 0)
            {
                var boneToReplace = sortedBones[0];
                sortedBones.RemoveAt(0);

                var addedBone = new AddedBone
                {
                    originalBoneIndex = boneToReplace,
                    newBoneIndex = nextBoneIndex++
                };
                shapePlan.Add(addedBone);

                remainingVertices.RemoveAll(v =>
                {
                    for (var bi = 0; bi < boneCounts[v]; bi++)
                    {
                        var boneWeight = boneWeights[bi + firstBoneIndex[v]];
                        if (boneWeight.weight == 0)
                            continue; // we're avoiding actual floating point zero here specifically
                        if (boneWeight.boneIndex < 0) continue;
                        if (boneWeight.boneIndex == boneToReplace)
                        {
                            boneWeight.boneIndex = addedBone.newBoneIndex;
                            boneWeights[bi + firstBoneIndex[v]] = boneWeight;
                            return true;
                        }
                    }
                    return false;
                });
            }

            return shapePlan;
        }
    }
}
