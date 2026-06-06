#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
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
        public const string NaNimatedBufferPrefix = "NaNimation buffer";
        public const string NaNimatedBonePrefix = "NaNimatedBone for ";
        
        public struct AddedBone
        {
            public int originalBoneIndex;
            public int newBoneIndex;
        }

        internal static Dictionary<(TargetProp, IVertexFilter), List<AddedBone>> ComputeNaNPlan(
            Renderer renderer,
            ref Mesh mesh,
            List<(TargetProp, IVertexFilter)> targets
        )
        {
            var vertexCount = mesh.vertexCount;

            if (vertexCount == 0)
            {
                // Nothing to do...
                return new ();
            }

            var originalMesh = mesh;
            mesh = Object.Instantiate(mesh);
            ObjectRegistry.RegisterReplacedObject(originalMesh, mesh);

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
            
            var affectedVertices = new bool[vertexCount];
            var firstBoneIndex = new int[vertexCount];

            var origBoneWeights = mesh.GetAllBoneWeights();
            var origBonesPerVertex = mesh.GetBonesPerVertex();

            var boneWeights =
                new NativeArray<BoneWeight1>(origBoneWeights.Length == 0 ? vertexCount : origBoneWeights.Length,
                    Allocator.Temp);
            var bonesPerVertex = new NativeArray<byte>(vertexCount, Allocator.Temp);

            int initialBoneCount = mesh.bindposeCount;
            try
            {
                if (origBoneWeights.Length == 0)
                {
                    // Generate new bone weights for a previously non-skinned mesh.
                    for (var i = 0; i < vertexCount; i++)
                    {
                        boneWeights[i] = new BoneWeight1
                        {
                            boneIndex = 0,
                            weight = 1f
                        };
                        bonesPerVertex[i] = 1;
                    }

                    initialBoneCount++;

                    mesh.bindposes = new[] { Matrix4x4.identity };

                    if (renderer is SkinnedMeshRenderer smr)
                    {
                        smr.bones = new[] { smr.transform };
                    }
                }
                else
                {
                    boneWeights.CopyFrom(origBoneWeights);
                    bonesPerVertex.CopyFrom(origBonesPerVertex);
                }

                var runningBoneIndex = 0;

                for (var v = 0; v < vertexCount; v++)
                {
                    firstBoneIndex[v] = runningBoneIndex;
                    runningBoneIndex += bonesPerVertex[v];
                }

                var nextBoneIndex = initialBoneCount;

                Dictionary<(TargetProp, IVertexFilter), List<AddedBone>> result = new();
                foreach (var (prop, filter) in targets)
                {
                    Array.Fill(affectedVertices, false);
                    filter.MarkFilteredVertices(renderer, mesh, affectedVertices);

                    if (!affectedVertices.Any(b => b)) continue;

                    var shapePlan = ComputeNaNPlanForShape(ref nextBoneIndex, boneWeights, bonesPerVertex,
                        firstBoneIndex,
                        affectedVertices);

                    if (shapePlan.Any())
                    {
                        result.Add((prop, filter), shapePlan);
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
            finally
            {
                boneWeights.Dispose();
                bonesPerVertex.Dispose();
            }
        }

        public static Dictionary<(TargetProp, IVertexFilter), List<GameObject>> GenerateNaNimatedBones(
            SkinnedMeshRenderer renderer,
            Dictionary<(TargetProp, IVertexFilter), List<AddedBone>> plan)
        {
            Dictionary<Transform, Transform> parentToBuffer = new();

            List<(AddedBone, (TargetProp, IVertexFilter))> createdBones =
                plan.SelectMany(kv => kv.Value.Select(bone => (bone, kv.Key)))
                    .OrderBy(b => b.bone.newBoneIndex)
                    .ToList();

            var maxNewBoneIndex = createdBones[^1].Item1.newBoneIndex;
            var bonesArray = new Transform[maxNewBoneIndex + 1];
            var curBonesArray = renderer.bones;
            Array.Copy(curBonesArray, 0, bonesArray, 0, curBonesArray.Length);

            // Special case for meshes with no bones; we need to create a bone 0
            if (curBonesArray.Length == 0)
            {
                bonesArray[0] = renderer.transform;
            }

            foreach (var pair in createdBones)
            {
                var bone = pair.Item1;
                var shape = pair.Item2;

                if (bonesArray[bone.originalBoneIndex] == null) continue;

                // When we merge armature after generating NaNimated bones, we can end up changing the localScale of
                // the nanimated bones, which is a problem, since we've baked that scale into our animation curves.
                //
                // To help avoid this, we add a buffer object - the buffer object will take the base scale change,
                // while NaNimated object goes between scale (1,1,1) and (NaN, NaN, NaN)
                //
                // See github bug: https://github.com/bdunderscore/modular-avatar/issues/1869

                if (!parentToBuffer.TryGetValue(bonesArray[bone.originalBoneIndex], out var bufferTransform))
                {
                    var bufferObj = new GameObject(NaNimatedBufferPrefix);
                    bufferTransform = bufferObj.transform;
                    bufferTransform.SetParent(bonesArray[bone.originalBoneIndex], false);
                    bufferTransform.localPosition = Vector3.zero;
                    bufferTransform.localRotation = Quaternion.identity;
                    bufferTransform.localScale = Vector3.one;
                    bufferObj.AddComponent<ModularAvatarPBBlocker>();

                    parentToBuffer.Add(bonesArray[bone.originalBoneIndex], bufferTransform);
                }

                var newBone = new GameObject(NaNimatedBonePrefix + shape.Item1.ToString().Replace('/', '_'));
                var newBoneTransform = newBone.transform;
                newBoneTransform.SetParent(bufferTransform, false);
                newBoneTransform.localPosition = Vector3.zero;
                newBoneTransform.localRotation = Quaternion.identity;
                newBoneTransform.localScale = Vector3.one;

                bonesArray[bone.newBoneIndex] = newBoneTransform;
            }

            renderer.bones = bonesArray;

            return plan.ToDictionary(kv => kv.Key,
                kv => kv.Value.Select(b => bonesArray[b.newBoneIndex].gameObject).ToList());
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
