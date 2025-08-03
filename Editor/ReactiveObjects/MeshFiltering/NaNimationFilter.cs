using System;
using System.Collections.Generic;
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
        
        internal static Dictionary<(TargetProp, IVertexFilter), string> ComputeNaNPlan(
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

            var deltaPositions = new Vector3[vertexCount];
            var affectedVertices = new bool[vertexCount];

            try
            {
                var nanindex = 0;

                Dictionary<(TargetProp, IVertexFilter), string> result = new();
                var newDeltas = new Vector3[vertexCount];
                var newNormals = new Vector3[vertexCount];
                var newTangents = new Vector3[vertexCount];

                foreach (var target in targets)
                {
                    var newShapeName = BlendShapeNamePrefix + (nanindex++);

                    Array.Fill(affectedVertices, true);
                    target.Item2.MarkFilteredVertices(renderer, mesh, affectedVertices);
                    
                    for (var v = 0; v < vertexCount; v++)
                    {
                        if (affectedVertices[v])
                        {
                            newDeltas[v] = new Vector3(float.PositiveInfinity, float.PositiveInfinity,
                                float.PositiveInfinity);
                        }
                    }

                    var index = mesh.blendShapeCount;
                    result.Add(target, newShapeName);
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