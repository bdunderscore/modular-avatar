using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Generates shape keys which effectively hide vertices. This is done by moving the vertices to +Infinity in all
    ///     directions, thus ensuring the final location will definitely be outside of the bounds of clip space. This then
    ///     allow us to hide arbitrary polygons in the mesh dynamically.
    ///
    ///     This technique was inspired by "NaNimation" (as originally described by
    ///     d4rk in https://github.com/d4rkc0d3r/d4rkAvatarOptimizer?tab=readme-ov-file#nanimation-toggles ).
    ///     However, NaNimation requires an animation; we can't set a bone scale to NaN in the initial prefab state of
    ///     the avatar, and thus this doesn't work when Safety settings block animations. Instead, by using a blendshape,
    ///     we can create a similar effect with better compatibility.
    ///
    ///     Note also that Unity does not allow NaN values in blendshapes, hence the use of +Infinity instead.
    /// </summary>
    internal static class NaNimationFilter
    {
        internal const string BlendShapeNamePrefix = "__modular-avatar_infinimation_";
        
        internal static Dictionary<string, string> ComputeNaNPlan(
            ref Mesh mesh,
            List<(string, float)> targetShapeNames,
            int initialBoneCount
        )
        {
            var vertexCount = mesh.vertexCount;

            if (vertexCount == 0)
            {
                // Nothing to do...
                return new Dictionary<string, string>();
            }

            var originalMesh = mesh;
            mesh = Object.Instantiate(mesh);
            ObjectRegistry.RegisterReplacedObject(originalMesh, mesh);

            var deltaPositions = new Vector3[vertexCount];
            var affectedVertices = new HashSet<int>(vertexCount);

            try
            {
                var nanindex = 0;

                Dictionary<string, string> result = new();
                var newDeltas = new Vector3[vertexCount];
                var newNormals = new Vector3[vertexCount];
                var newTangents = new Vector3[vertexCount];

                foreach (var (shapeName, threshold) in targetShapeNames)
                {
                    var newShapeName = BlendShapeNamePrefix + (nanindex++);

                    var sqrThreshold = threshold * threshold;
                    var shape = mesh.GetBlendShapeIndex(shapeName);
                    if (shape < 0) continue; // shape not found

                    affectedVertices.Clear();

                    Array.Fill(newDeltas, Vector3.zero);
                    Array.Fill(newNormals, Vector3.zero);
                    Array.Fill(newTangents, Vector3.zero);

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
                                newDeltas[v] = new Vector3(float.PositiveInfinity, float.PositiveInfinity,
                                    float.PositiveInfinity);
                            }
                        }
                    }

                    if (!anyAffected) continue;

                    var index = mesh.blendShapeCount;
                    result.Add(shapeName, newShapeName);
                    mesh.AddBlendShapeFrame(newShapeName, 1.0f, newDeltas, newNormals, newTangents);
                    mesh.GetBlendShapeFrameVertices(index, 0, newDeltas, newNormals, newTangents);
                }

                return result;
            }
            finally
            {
                // TODO - remove dummy finally block after merging the Delete by Mask changes
            }
        }
    }
}