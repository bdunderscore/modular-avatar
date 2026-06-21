#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class RemoveVerticesFromMesh
    {
        public static Mesh RemoveVertices(Renderer renderer, Mesh original,
            IEnumerable<(TargetProp, IMeshSelector)> targets)
        {
            var mesh = new Mesh();
            mesh.indexFormat = original.indexFormat;
            mesh.bounds = original.bounds;

            int submeshCount = original.subMeshCount;
            var primitiveMasks = new NativeArray<bool>[submeshCount];
            var retainMask = new NativeArray<bool>(original.vertexCount, Allocator.TempJob);

            try
            {
                using var selectorJob = new MeshSelectorJob(renderer, original);
                
                var selectors = targets.Select(t => t.Item2).ToList();
                IMeshSelector combinedSelector = selectors.Count == 0 ? null
                    : selectors.Count == 1 ? selectors[0]
                    : new ORFilter(selectors);

                // Phase 1: schedule primitive mask jobs
                var primMaskHandles = new JobHandle[submeshCount];
                for (int sm = 0; sm < submeshCount; sm++)
                {
                    var desc = selectorJob.MeshData.GetSubMesh(sm);
                    int vertsPerPrim = VertsPerPrim(desc.topology);
                    primitiveMasks[sm] = new NativeArray<bool>(desc.indexCount / vertsPerPrim, Allocator.TempJob);
                    if (combinedSelector != null)
                        primMaskHandles[sm] = combinedSelector.MarkFilteredPrimitives(
                            selectorJob, sm, primitiveMasks[sm]);
                }

                // Phase 2: build retain mask using the already-adjusted index buffers.
                // Jobs are chained so writes to retainMask don't race across submeshes.
                JobHandle retainChain = default;
                for (int sm = 0; sm < submeshCount; sm++)
                {
                    var (indexBuffer, indexDep) = selectorJob.GetSubmeshIndexBuffer(sm);
                    int vertsPerPrim = VertsPerPrim(selectorJob.MeshData.GetSubMesh(sm).topology);
                    var deps = JobHandle.CombineDependencies(indexDep, primMaskHandles[sm], retainChain);
                    retainChain = new MarkRetainedVerticesJob
                    {
                        IndexBuffer = indexBuffer,
                        PrimitiveMask = primitiveMasks[sm],
                        PrimSize = vertsPerPrim,
                        RetainMask = retainMask
                    }.Schedule(primitiveMasks[sm].Length, 64, deps);
                }

                retainChain.Complete();

                if (!retainMask.Any(b => b)) retainMask[0] = true;

                RemapVerts(retainMask, out var origToNewVertIndex, out var newToOrigVertIndex);

                TransferVertexData(mesh, original, retainMask);
                mesh.bindposes = original.bindposes;
                TransferShapes(mesh, original, newToOrigVertIndex);
                UpdateTriangles(mesh, selectorJob, primitiveMasks, origToNewVertIndex);
            }
            finally
            {
                retainMask.Dispose();
                foreach (var mask in primitiveMasks)
                    if (mask.IsCreated) mask.Dispose();
            }

            return mesh;
        }

        /// <summary>
        ///     Preview-optimized path: clones the mesh and filters only the index buffers, keeping all vertices intact.
        /// </summary>
        public static Mesh FilterPrimitivesOnly(Renderer renderer, Mesh original, IEnumerable<IMeshSelector> selectors)
        {
            var mesh = Object.Instantiate(original);

            var submeshCount = original.subMeshCount;
            var primitiveMasks = new NativeArray<bool>[submeshCount];

            try
            {
                using var selectorJob = new MeshSelectorJob(renderer, original);

                var selectorList = selectors.ToList();
                var combinedSelector = selectorList.Count == 0 ? null
                    : selectorList.Count == 1 ? selectorList[0]
                    : new ORFilter(selectorList);

                var primMaskHandles = new JobHandle[submeshCount];
                for (var sm = 0; sm < submeshCount; sm++)
                {
                    var desc = selectorJob.MeshData.GetSubMesh(sm);
                    var vertsPerPrim = VertsPerPrim(desc.topology);
                    primitiveMasks[sm] = new NativeArray<bool>(desc.indexCount / vertsPerPrim, Allocator.TempJob);
                    if (combinedSelector != null)
                        primMaskHandles[sm] = combinedSelector.MarkFilteredPrimitives(
                            selectorJob, sm, primitiveMasks[sm]);
                }

                for (var sm = 0; sm < submeshCount; sm++)
                    primMaskHandles[sm].Complete();

                var origToNewVertIndex = new int[original.vertexCount];
                for (var i = 0; i < origToNewVertIndex.Length; i++) origToNewVertIndex[i] = i;
                UpdateTriangles(mesh, selectorJob, primitiveMasks, origToNewVertIndex);
            }
            finally
            {
                foreach (var mask in primitiveMasks)
                    if (mask.IsCreated)
                        mask.Dispose();
            }

            return mesh;
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

        [BurstCompile]
        private struct MarkRetainedVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> IndexBuffer;
            [ReadOnly] public NativeArray<bool> PrimitiveMask;
            // Multiple threads may write true to the same vertex index; writes are idempotent.
            public int PrimSize;
            [NativeDisableParallelForRestriction] public NativeArray<bool> RetainMask;

            public void Execute(int primIndex)
            {
                if (PrimitiveMask[primIndex]) return;
                for (int v = 0; v < PrimSize; v++)
                    RetainMask[IndexBuffer[primIndex * PrimSize + v]] = true;
            }
        }

        private static void TransferVertexData(Mesh mesh, Mesh original, NativeArray<bool> toRetain)
        {
            var newToOriginal = new List<int>(toRetain.Length);
            for (var i = 0; i < toRetain.Length; i++)
                if (toRetain[i])
                    newToOriginal.Add(i);
            MeshVertexCopyUtil.TransferVertexData(mesh, original, newToOriginal.ToArray());
        }

        private static void TransferShapes(Mesh mesh, Mesh original, int[] newToOrigVertIndex)
        {
            MeshVertexCopyUtil.TransferShapes(mesh, original, newToOrigVertIndex);
        }

        private static void UpdateTriangles(Mesh mesh, MeshSelectorJob selectorJob,
            NativeArray<bool>[] primitiveMasks, int[] origToNewVertIndex)
        {
            int submeshCount = primitiveMasks.Length;
            var newIndices = new List<int>();
            var newIndices16 = new List<ushort>();

            mesh.subMeshCount = submeshCount;

            for (int sm = 0; sm < submeshCount; sm++)
            {
                var smDesc = selectorJob.MeshData.GetSubMesh(sm);
                int vertsPerPrim = VertsPerPrim(smDesc.topology);
                newIndices.Clear();
                newIndices16.Clear();

                var (indexBuffer, jobHandle) = selectorJob.GetSubmeshIndexBuffer(sm);
                // This _should_ be done by now, but just to avoid any mistakes we'll ensure it's completed at the
                // pointer where we fetch it.
                jobHandle.Complete();
                var mask = primitiveMasks[sm];

                for (int p = 0; p < mask.Length; p++)
                {
                    if (mask[p]) continue; // deleted
                    for (int v = 0; v < vertsPerPrim; v++)
                        newIndices.Add(origToNewVertIndex[indexBuffer[p * vertsPerPrim + v]]);
                }

                if (newIndices.Count == 0)
                {
                    // Add a degenerate primitive to avoid creating an empty submesh.
                    // TODO: Perform necessary animation updates to allow us to delete the submesh entirely.
                    for (int v = 0; v < vertsPerPrim; v++) newIndices.Add(0);
                }

                if (mesh.indexFormat == IndexFormat.UInt16)
                {
                    var minVertex = Math.Max(0, newIndices.Min());
                    foreach (var i in newIndices) newIndices16.Add((ushort)(i - minVertex));
                    mesh.SetIndices(newIndices16, 0, newIndices16.Count, smDesc.topology, sm, true, minVertex);
                }
                else
                {
                    // don't bother computing min vertex for UInt32 indices, as it will always fit anyway
                    mesh.SetIndices(newIndices, 0, newIndices.Count, smDesc.topology, sm);
                }
            }
        }

        private static void RemapVerts(NativeArray<bool> toRetainVertices, out int[] origToNewVertIndex,
            out int[] newToOrigVertIndex)
        {
            List<int> n2o = new List<int>(toRetainVertices.Length);
            List<int> o2n = new List<int>(toRetainVertices.Length);

            for (int j = 0; j < toRetainVertices.Length; j++)
            {
                if (toRetainVertices[j])
                {
                    o2n.Add(n2o.Count);
                    n2o.Add(j);
                }
                else
                {
                    o2n.Add(-1);
                }
            }

            newToOrigVertIndex = n2o.ToArray();
            origToNewVertIndex = o2n.ToArray();
        }
    }
}
