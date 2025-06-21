using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Manipulates the bone weights of a mesh to prepare to hide vertices using "NaNimation" (as originally described by
    ///     d4rk in https://github.com/d4rkc0d3r/d4rkAvatarOptimizer?tab=readme-ov-file#nanimation-toggles ).
    ///     NaNimation works by animating the scale of bones to NaN, in order to cause any primitives including those vertices
    ///     to be hidden. This allows for arbitrary sections of the mesh to be hidden without needing specific shader support.
    ///     (Note that we currently only support using blendshapes to select which vertices to hide, but this could be
    ///     extended in the future).
    /// </summary>
    internal static class NaNimationFilter
    {
        public struct AddedBone
        {
            public int originalBoneIndex;
            public int newBoneIndex;
        }

        internal static Dictionary<string, List<AddedBone>> ComputeNaNPlan(
            ref Mesh mesh,
            List<(string,float)> targetShapeNames,
            int initialBoneCount
        )
        {
            mesh = Object.Instantiate(mesh);

            /*
             * Our high level algorithm is as follows:
             *
             * For each NaNimated shape, we need one or more new bones, such that we fully cover all primitives in the shape.
             * ... however mapping to primitives is expensive, so we just map to vertices instead. This might result in
             * overcoverage (and thus extra bones), but in most cases shrink shapes are closely bound to bones, so it
             * shouldn't be a large problem, and greatly reduces the complexity of the algorithm.
             *
             * Therefore, for each shape we select the bone covering the most vertices, and add it to the NaNimation plan.
             * If the bone covers all vertices, we can stop; otherwise, we repeat the process until all vertices are covered.
             *
             * If multiple shapes cover the same vertex, we can (but are not required to) use the same original bone
             * for both shapes. In this case, the NaNimated bones will be nested.
             */

            var vertexCount = mesh.vertexCount;
            var deltaPositions = new Vector3[vertexCount];
            var affectedVertices = new HashSet<int>(vertexCount);
            var firstBoneIndex = new int[vertexCount];

            var origBoneWeights = mesh.GetAllBoneWeights();
            var origBonesPerVertex = mesh.GetBonesPerVertex();

            using var boneWeights = new NativeArray<BoneWeight1>(origBoneWeights.Length, Allocator.Temp);
            using var bonesPerVertex = new NativeArray<byte>(origBonesPerVertex.Length, Allocator.Temp);

            boneWeights.CopyFrom(origBoneWeights);
            bonesPerVertex.CopyFrom(origBonesPerVertex);

            var runningBoneIndex = 0;

            for (var v = 0; v < vertexCount; v++)
            {
                firstBoneIndex[v] = runningBoneIndex;
                runningBoneIndex += origBonesPerVertex[v];
            }

            var nextBoneIndex = initialBoneCount;

            Dictionary<string, List<AddedBone>> result = new();
            foreach ((var shapeName, var threshold) in targetShapeNames)
            {
                var sqrThreshold = threshold * threshold;
                var shape = mesh.GetBlendShapeIndex(shapeName);
                if (shape < 0) continue; // shape not found

                affectedVertices.Clear();

                var frameCount = mesh.GetBlendShapeFrameCount(shape);
                var anyAffected = false;
                for (var i = 0; i < frameCount; i++)
                {
                    mesh.GetBlendShapeFrameVertices(shape, i, deltaPositions, null, null);

                    for (var v = 0; v < vertexCount; v++)
                    {
                        if (deltaPositions[v].sqrMagnitude > sqrThreshold)
                        {
                            affectedVertices.Add(v);
                            anyAffected = true;
                        }
                    }
                }

                if (!anyAffected) continue;

                var shapePlan = ComputeNaNPlanForShape(ref nextBoneIndex, boneWeights, bonesPerVertex, firstBoneIndex,
                    affectedVertices);

                if (shapePlan.Any())
                {
                    result.Add(shapeName, shapePlan);
                }
            }

            var bindposes = mesh.bindposes;
            Array.Resize(ref bindposes, nextBoneIndex);

            foreach (var addedBone in result.SelectMany(kv => kv.Value).OrderBy(b => b.originalBoneIndex))
            {
                bindposes[addedBone.newBoneIndex] = bindposes[addedBone.originalBoneIndex];
            }

            mesh.bindposes = bindposes;

            mesh.SetBoneWeights(bonesPerVertex, boneWeights);

            return result;
        }

        private static List<AddedBone> ComputeNaNPlanForShape(
            ref int nextBoneIndex,
            NativeArray<BoneWeight1> boneWeights,
            NativeArray<byte> boneCounts,
            int[] firstBoneIndex,
            HashSet<int> vertexMask
        )
        {
            var boneToVertexCount = new Dictionary<int, int>();
            var remainingVertices = new List<int>();

            foreach (var v in vertexMask)
            {
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